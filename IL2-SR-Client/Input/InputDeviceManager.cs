using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Network;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Settings;
using Ciribob.IL2.SimpleRadio.Standalone.Common;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.IL2.SimpleRadio.Standalone.Client.UI;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Utils;
using NLog;
using SharpDX.DirectInput;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.Settings
{
    public class InputDeviceManager : IDisposable
    {
        public delegate void DetectButton(InputDevice inputDevice);

        public delegate void DetectPttCallback(List<InputBindState> buttonStates);

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static HashSet<Guid> _blacklistedDevices = new HashSet<Guid>
        {
            new Guid("1b171b1c-0000-0000-0000-504944564944"),
            //Corsair K65 Gaming keyboard  It reports as a Joystick when its a keyboard...
            new Guid("1b091b1c-0000-0000-0000-504944564944"), // Corsair K70R Gaming Keyboard
            new Guid("1b1e1b1c-0000-0000-0000-504944564944"), //Corsair Gaming Scimitar RGB Mouse
            new Guid("16a40951-0000-0000-0000-504944564944"), //HyperX 7.1 Audio
            new Guid("b660044f-0000-0000-0000-504944564944"), // T500 RS Gear Shift
            new Guid("00f2068e-0000-0000-0000-504944564944") //CH PRO PEDALS USB
        };

        //devices that report incorrectly but SHOULD work?
        public static HashSet<Guid> _whitelistDevices = new HashSet<Guid>
        {
            new Guid("1105231d-0000-0000-0000-504944564944"), //GTX Throttle
            new Guid("b351044f-0000-0000-0000-504944564944"), //F16 MFD 2 Usage: Generic Type: Supplemental
            new Guid("11401dd2-0000-0000-0000-504944564944"), //Leo Bodnar BUtton Box
            new Guid("204803eb-0000-0000-0000-504944564944"), // VPC Throttle
            new Guid("204303eb-0000-0000-0000-504944564944"), // VPC Stick
            new Guid("205403eb-0000-0000-0000-504944564944"), // VPC Throttle
            new Guid("205603eb-0000-0000-0000-504944564944"), // VPC Throttle
            new Guid("205503eb-0000-0000-0000-504944564944")  // VPC Throttle

        };

        private readonly DirectInput _directInput;
        private readonly object _inputDevicesLock = new object();
        private readonly Dictionary<Guid, Device> _inputDevices = new Dictionary<Guid, Device>();
        private readonly DeviceIdentityCache<Device> _deviceIdentities = new DeviceIdentityCache<Device>(ReadDeviceIdentity);
        // Bound devices not found during the current PTT poll (PTT thread only), named in the slow-poll warning.
        private readonly HashSet<string> _missingBoundDevicesThisPoll = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly MainWindow.ToggleOverlayCallback _toggleOverlayCallback;
        private readonly Action _togglePilotRosterCallback;
        private readonly Action _toggleClientListCallback;
        private readonly Action _restartSrsCallback;
        private readonly IntPtr _windowHandle;

        private volatile bool _detectPtt;
        private DateTime _nextDeviceReconnectAttemptUtc = DateTime.MinValue;
        private int _currentDeviceReconnectRetryMs = MinDeviceReconnectRetryMs;
        private const int MinDeviceReconnectRetryMs = 2000;
        private const int MaxDeviceReconnectRetryMs = 30000;
        private const int SlowPttInputPollWarningMs = 750;
        private long _lastPttInputPollUtcTicks = DateTime.UtcNow.Ticks;
        private long _lastSlowPttInputPollLogTicks;

        public DateTime LastPttInputPollUtc => new DateTime(Interlocked.Read(ref _lastPttInputPollUtcTicks), DateTimeKind.Utc);

        //used to trigger the update to a frequency
        private InputBinding _lastActiveBinding = InputBinding.ModifierIntercom
            ; //intercom used to represent null as we cant
        private bool _pilotRosterTogglePressed;
        private bool _clientListTogglePressed;

        // Volume axis state (PTT thread only, so unlocked).
        private readonly Dictionary<InputBinding, float> _lastAxisVolumes = new Dictionary<InputBinding, float>();
        private readonly Dictionary<InputBinding, RadioInformation> _lastAxisRadios = new Dictionary<InputBinding, RadioInformation>();
        private readonly AxisReportWatch<Device> _axisReportWatch = new AxisReportWatch<Device>();

        private Settings.GlobalSettingsStore _globalSettings = Settings.GlobalSettingsStore.Instance;


        public InputDeviceManager(Window window, MainWindow.ToggleOverlayCallback toggleOverlayCallback,
            Action togglePilotRosterCallback, Action toggleClientListCallback, Action restartSrsCallback)
        {
            _directInput = new DirectInput();


            _windowHandle = new WindowInteropHelper(window).Handle;

            _toggleOverlayCallback = toggleOverlayCallback;
            _togglePilotRosterCallback = togglePilotRosterCallback;
            _toggleClientListCallback = toggleClientListCallback;
            _restartSrsCallback = restartSrsCallback;

            LoadWhiteList();

            LoadBlackList();

            InitDevices();


        }

        public void InitDevices()
        {
            InitDevices(false);
        }

        public void InitDevices(bool refreshControllerDevices)
        {
            InitDevicesInternal(refreshControllerDevices);
        }

        private int InitDevicesInternal(bool refreshControllerDevices)
        {
            Logger.Info("Starting Device Search. Expand Search: " +
            (_globalSettings.GetClientSettingBool(GlobalSettingsKeys.ExpandControls)));

            var deviceInstances = _directInput.GetDevices();
            var changedDevices = 0;

            lock (_inputDevicesLock)
            {
                foreach (var staleDevice in _inputDevices.Where(kpDevice => kpDevice.Value == null || kpDevice.Value.IsDisposed).Select(kpDevice => kpDevice.Key).ToList())
                {
                    _inputDevices.Remove(staleDevice);
                }

                foreach (var deviceInstance in deviceInstances)
                {
                    try
                    {
                        //Workaround for Bad Devices that pretend to be joysticks
                        if (IsBlackListed(deviceInstance.ProductGuid))
                        {
                            Logger.Info("Found but ignoring blacklist device  " + deviceInstance.ProductGuid + " Instance: " +
                                deviceInstance.InstanceGuid + " " +
                                deviceInstance.ProductName.Trim().Replace("\0", "") + " Type: " + deviceInstance.Type);
                            continue;
                        }

                        Logger.Info("Found Device ID:" + deviceInstance.ProductGuid +
                                    " " +
                                    deviceInstance.ProductName.Trim().Replace("\0", "") + " Usage: " +
                                    deviceInstance.UsagePage + " Type: " +
                                    deviceInstance.Type);
                        var isControllerDevice = IsControllerDevice(deviceInstance);
                        var isExpandedControllerDevice = !isControllerDevice &&
                                                         GlobalSettingsStore.Instance.GetClientSettingBool(GlobalSettingsKeys.ExpandControls);
                        if (_inputDevices.ContainsKey(deviceInstance.InstanceGuid))
                        {
                            if (refreshControllerDevices && (isControllerDevice || isExpandedControllerDevice))
                            {
                                Logger.Info("Refreshing input device after reconnect attempt:" + deviceInstance.ProductGuid +
                                            " " +
                                            deviceInstance.ProductName.Trim().Replace("\0", ""));

                                RemoveInputDeviceLocked(deviceInstance.InstanceGuid);
                                changedDevices++;
                            }
                            else
                            {
                            Logger.Info("Already have device:" + deviceInstance.ProductGuid +
                                        " " +
                                        deviceInstance.ProductName.Trim().Replace("\0", ""));
                            continue;
                        }
                        }


                        if (deviceInstance.Type == DeviceType.Keyboard)
                        {

                            Logger.Info("Adding Device ID:" + deviceInstance.ProductGuid +
                                        " " +
                                        deviceInstance.ProductName.Trim().Replace("\0", ""));
                            var device = new Keyboard(_directInput);

                            device.SetCooperativeLevel(_windowHandle,
                                CooperativeLevel.Background | CooperativeLevel.NonExclusive);
                            device.Acquire();

                            _inputDevices.Add(deviceInstance.InstanceGuid, device);
                            changedDevices++;
                        }
                        else if (deviceInstance.Type == DeviceType.Mouse)
                        {
                            Logger.Info("Adding Device ID:" + deviceInstance.ProductGuid + " " +
                                        deviceInstance.ProductName.Trim().Replace("\0", ""));
                            var device = new Mouse(_directInput);

                            device.SetCooperativeLevel(_windowHandle,
                                CooperativeLevel.Background | CooperativeLevel.NonExclusive);
                            device.Acquire();

                            _inputDevices.Add(deviceInstance.InstanceGuid, device);
                            changedDevices++;
                        }
                        else if (isControllerDevice)
                        {
                            var device = new Joystick(_directInput, deviceInstance.InstanceGuid);

                            Logger.Info("Adding ID:" + deviceInstance.ProductGuid + " " +
                                        deviceInstance.ProductName.Trim().Replace("\0", ""));

                            device.SetCooperativeLevel(_windowHandle,
                                CooperativeLevel.Background | CooperativeLevel.NonExclusive);
                            SetAxisRange(device);
                            device.Acquire();

                            _inputDevices.Add(deviceInstance.InstanceGuid, device);
                            changedDevices++;
                        }
                        else if (isExpandedControllerDevice)
                        {
                            Logger.Info("Adding (Expanded Devices) ID:" + deviceInstance.ProductGuid + " " +
                                        deviceInstance.ProductName.Trim().Replace("\0", ""));

                            var device = new Joystick(_directInput, deviceInstance.InstanceGuid);

                            device.SetCooperativeLevel(_windowHandle,
                                CooperativeLevel.Background | CooperativeLevel.NonExclusive);
                            SetAxisRange(device);
                            device.Acquire();

                            _inputDevices.Add(deviceInstance.InstanceGuid, device);
                            changedDevices++;

                            Logger.Info("Added (Expanded Device) ID:" + deviceInstance.ProductGuid + " " +
                                        deviceInstance.ProductName.Trim().Replace("\0", ""));
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "Unable to initialise input device " + deviceInstance.ProductGuid + " " +
                                         deviceInstance.ProductName.Trim().Replace("\0", "") + ", will retry on the next device rediscovery");
                    }
                }
            }

            return changedDevices;
        }

        private void LoadWhiteList()
        {
            var path = UserDataPaths.GetPath("whitelist.txt");
            Logger.Info("Attempt to Load Whitelist from " + path);

            LoadGuidFromPath(path, _whitelistDevices);
        }

        private void LoadBlackList()
        {
            var path = UserDataPaths.GetPath("blacklist.txt");
            Logger.Info("Attempt to Load Blacklist from " + path);

            LoadGuidFromPath(path, _blacklistedDevices);
        }

        private void LoadGuidFromPath(string path, HashSet<Guid> _hashSet)
        {
            if (!File.Exists(path))
            {
                Logger.Info("File doesnt exist: " + path);
                return;
            }

            string[] lines = File.ReadAllLines(path);
            if (lines?.Length <= 0)
            {
                return;

            }

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.Length > 0)
                {
                    try
                    {
                        _hashSet.Add(new Guid(trimmed));
                        Logger.Info("Added " + trimmed);
                    }
                    catch (Exception ex)
                    {
                    }
                }
            }
        }

        public void Dispose()
        {
            StopPtt();
            lock (_inputDevicesLock)
            {
                foreach (var kpDevice in _inputDevices)
                {
                    if (kpDevice.Value != null)
                    {
                        kpDevice.Value.Unacquire();
                        kpDevice.Value.Dispose();
                    }
                }

                _inputDevices.Clear();
            }
        }

        public bool IsBlackListed(Guid device)
        {
            return _blacklistedDevices.Contains(device);
        }

        public bool IsWhiteListed(Guid device)
        {
            return _whitelistDevices.Contains(device);
        }

        private bool IsControllerDevice(DeviceInstance deviceInstance)
        {
            return ((deviceInstance.Type >= DeviceType.Joystick) &&
                    (deviceInstance.Type <= DeviceType.FirstPerson)) ||
                   IsWhiteListed(deviceInstance.ProductGuid);
        }

        public void AssignButton(DetectButton callback)
        {
            //detect the state of all current buttons
            Task.Run(() =>
            {
                var deviceList = GetInputDeviceSnapshot();

                var initial = new int[deviceList.Count, 128 + 4]; // for POV

                for (var i = 0; i < deviceList.Count; i++)
                {
                    if (deviceList[i] == null || deviceList[i].IsDisposed)
                    {
                        continue;
                    }

                    try
                    {
                        if (deviceList[i] is Joystick)
                        {
                            deviceList[i].Poll();

                            var state = (deviceList[i] as Joystick).GetCurrentState();

                            for (var j = 0; j < state.Buttons.Length; j++)
                            {
                                initial[i, j] = state.Buttons[j] ? 1 : 0;
                            }
                            var pov = state.PointOfViewControllers;

                            for (var j = 0; j < pov.Length; j++)
                            {
                                initial[i, j + 128] = pov[j];
                            }
                        }
                        else if (deviceList[i] is Keyboard)
                        {
                            var keyboard = deviceList[i] as Keyboard;
                            keyboard.Poll();
                            var state = keyboard.GetCurrentState();

                            for (var j = 0; j < 128; j++)
                            {
                                initial[i, j] = state.IsPressed(state.AllKeys[j]) ? 1 : 0;
                            }
                        }
                        else if (deviceList[i] is Mouse)
                        {
                            var mouse = deviceList[i] as Mouse;
                            mouse.Poll();

                            var state = mouse.GetCurrentState();

                            for (var j = 0; j < state.Buttons.Length; j++)
                            {
                                initial[i, j] = state.Buttons[j] ? 1 : 0;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, $"Failed to get current state of input device {deviceList[i].Information.ProductName.Trim().Replace("\0", "")} " +
                            $"(ID: {deviceList[i].Information.ProductGuid}) while assigning button, ignoring until next restart/rediscovery");

                        deviceList[i].Unacquire();
                        deviceList[i].Dispose();
                        deviceList[i] = null;
                    }
                }

                var device = string.Empty;
                var button = 0;
                var deviceGuid = Guid.Empty;
                var buttonValue = -1;
                var found = false;

                while (!found)
                {
                    Thread.Sleep(100);

                    for (var i = 0; i < deviceList.Count; i++)
                    {
                        if (deviceList[i] == null || deviceList[i].IsDisposed)
                        {
                            continue;
                        }

                        try
                        {
                            if (deviceList[i] is Joystick)
                            {
                                deviceList[i].Poll();

                                var state = (deviceList[i] as Joystick).GetCurrentState();

                                for (var j = 0; j < 128 + 4; j++)
                                {
                                    if (j >= 128)
                                    {
                                        //handle POV
                                        var pov = state.PointOfViewControllers;

                                        if (pov[j - 128] != initial[i, j])
                                        {
                                            found = true;

                                            var inputDevice = new InputDevice
                                            {
                                                DeviceName =
                                                    deviceList[i].Information.ProductName.Trim().Replace("\0", ""),
                                                Button = j,
                                                InstanceGuid = deviceList[i].Information.InstanceGuid,
                                                ProductGuid = deviceList[i].Information.ProductGuid,
                                                ButtonValue = pov[j - 128]
                                            };
                                            Application.Current.Dispatcher.Invoke(
                                                () => { callback(inputDevice); });
                                            return;
                                        }
                                    }
                                    else
                                    {
                                        var buttonState = state.Buttons[j] ? 1 : 0;

                                        if (buttonState != initial[i, j])
                                        {
                                            found = true;

                                            var inputDevice = new InputDevice
                                            {
                                                DeviceName =
                                                    deviceList[i].Information.ProductName.Trim().Replace("\0", ""),
                                                Button = j,
                                                InstanceGuid = deviceList[i].Information.InstanceGuid,
                                                ProductGuid = deviceList[i].Information.ProductGuid,
                                                ButtonValue = buttonState
                                            };

                                            Application.Current.Dispatcher.Invoke(
                                                () => { callback(inputDevice); });


                                            return;
                                        }
                                    }
                                }
                            }
                            else if (deviceList[i] is Keyboard)
                            {
                                var keyboard = deviceList[i] as Keyboard;
                                keyboard.Poll();
                                var state = keyboard.GetCurrentState();

                                for (var j = 0; j < 128; j++)
                                {
                                    if (initial[i, j] != (state.IsPressed(state.AllKeys[j]) ? 1 : 0))
                                    {
                                        found = true;

                                        var inputDevice = new InputDevice
                                        {
                                            DeviceName =
                                                deviceList[i].Information.ProductName.Trim().Replace("\0", ""),
                                            Button = j,
                                            InstanceGuid = deviceList[i].Information.InstanceGuid,
                                            ProductGuid = deviceList[i].Information.ProductGuid,
                                            ButtonValue = 1
                                        };

                                        Application.Current.Dispatcher.Invoke(
                                            () => { callback(inputDevice); });


                                        return;
                                    }

                                    //                                if (initial[i, j] == 1)
                                    //                                {
                                    //                                    Console.WriteLine("Pressed: "+j);
                                    //                                    MessageBox.Show("Keyboard!");
                                    //                                }
                                }
                            }
                            else if (deviceList[i] is Mouse)
                            {
                                deviceList[i].Poll();

                                var state = (deviceList[i] as Mouse).GetCurrentState();

                                //skip left mouse button - start at 1 with j 0 is left, 1 is right, 2 is middle
                                for (var j = 1; j < state.Buttons.Length; j++)
                                {
                                    var buttonState = state.Buttons[j] ? 1 : 0;

                                    if (buttonState != initial[i, j])
                                    {
                                        found = true;

                                        var inputDevice = new InputDevice
                                        {
                                            DeviceName =
                                                deviceList[i].Information.ProductName.Trim().Replace("\0", ""),
                                            Button = j,
                                            InstanceGuid = deviceList[i].Information.InstanceGuid,
                                            ProductGuid = deviceList[i].Information.ProductGuid,
                                            ButtonValue = buttonState
                                        };

                                        Application.Current.Dispatcher.Invoke(
                                            () => { callback(inputDevice); });
                                        return;
                                    }
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            Logger.Error(e, $"Failed to get current state of input device {deviceList[i].Information.ProductName.Trim().Replace("\0", "")} " +
                                $"(ID: {deviceList[i].Information.ProductGuid}) while discovering button press while assigning, ignoring until next restart/rediscovery");

                            deviceList[i].Unacquire();
                            deviceList[i].Dispose();
                            deviceList[i] = null;
                        }
                    }
                }
            });
        }


        public void StartDetectPtt(DetectPttCallback callback)
        {
            _detectPtt = true;
            //detect the state of all current buttons
            var pttInputThread = new Thread(() =>
            {
                while (_detectPtt)
                {
                    MarkPttInputPoll();
                    _missingBoundDevicesThisPoll.Clear();
                    var pollStopwatch = Stopwatch.StartNew();
                    try
                    {
                        var bindStates = GenerateBindStateList();
                        var deviceStateCache = new Dictionary<Guid, object>();

                        for (var i = 0; i < bindStates.Count; i++)
                        {
                            //contains main binding and optional modifier binding + states of each
                            var bindState = bindStates[i];

                            bindState.MainDeviceState = GetButtonState(bindState.MainDevice, deviceStateCache);

                            if (bindState.ModifierDevice != null)
                            {
                                bindState.ModifierState = GetButtonState(bindState.ModifierDevice, deviceStateCache);

                                bindState.IsActive = bindState.MainDeviceState && bindState.ModifierState;
                            }
                            else
                            {
                                bindState.IsActive = bindState.MainDeviceState;
                            }

                            //now check this is the best binding and no previous ones are better
                            //Means you can have better binds like PTT  = Space and Radio 1 is Space +1 - holding space +1 will actually trigger radio 1 not PTT
                            if (bindState.IsActive)
                            {
                                for (int j = 0; j < i; j++)
                                {
                                    //check previous bindings
                                    var previousBind = bindStates[j];

                                    if (!previousBind.IsActive)
                                    {
                                        continue;
                                    }

                                    if (previousBind.ModifierDevice == null && bindState.ModifierDevice != null)
                                    {
                                        //set previous bind to off if previous bind Main == main or modifier of bindstate
                                        if (previousBind.MainDevice.IsSameBind(bindState.MainDevice))
                                        {
                                            previousBind.IsActive = false;
                                            break;
                                        }
                                        if (previousBind.MainDevice.IsSameBind(bindState.ModifierDevice))
                                        {
                                            previousBind.IsActive = false;
                                            break;
                                        }
                                    }
                                    else if (previousBind.ModifierDevice != null && bindState.ModifierDevice == null)
                                    {
                                        if (previousBind.MainDevice.IsSameBind(bindState.MainDevice))
                                        {
                                            bindState.IsActive = false;
                                            break;
                                        }
                                        if (previousBind.ModifierDevice.IsSameBind(bindState.MainDevice))
                                        {
                                            bindState.IsActive = false;
                                            break;
                                        }
                                    }
                                }
                            }
                        }

                    callback(bindStates);
                    MarkPttInputPoll();
                    WarnIfPttInputPollIsSlow(pollStopwatch.ElapsedMilliseconds, bindStates.Count);
                    HandleWindowToggleBindings(bindStates);
                    //handle overlay

                    foreach (var bindState in bindStates)
                    {
                        if (bindState.IsActive && bindState.MainDevice.InputBind == InputBinding.OverlayToggle)
                        {
                            //run on main
                            Application.Current.Dispatcher.Invoke(
                                () => { _toggleOverlayCallback(false); });
                            break;
                        }
                        else if (bindState.MainDevice.InputBind == InputBinding.RestartSrs)
                        {
                            if (bindState.MainDevice.InputBind == _lastActiveBinding && !bindState.IsActive)
                            {
                                _lastActiveBinding = InputBinding.ModifierIntercom;
                            }

                            if (bindState.IsActive && bindState.MainDevice.InputBind != _lastActiveBinding)
                            {
                                _lastActiveBinding = bindState.MainDevice.InputBind;

                                Application.Current.Dispatcher.BeginInvoke(
                                    new Action(() => { _restartSrsCallback?.Invoke(); }));
                                break;
                            }
                        }
                        else if (IsRadioControlBinding(bindState.MainDevice.InputBind))
                        {
                            if (bindState.MainDevice.InputBind == _lastActiveBinding && !bindState.IsActive)
                            {
                                //Assign to a totally different binding to mark as unassign
                                _lastActiveBinding = InputBinding.ModifierIntercom;
                            }

                            //key repeat
                            if (bindState.IsActive && (bindState.MainDevice.InputBind != _lastActiveBinding))
                            {
                                _lastActiveBinding = bindState.MainDevice.InputBind;

                                var IL2PlayerRadioInfo = ClientStateSingleton.Instance.PlayerGameState;

                                if (IL2PlayerRadioInfo != null )
                                {
                                    switch (bindState.MainDevice.InputBind)
                                    {
                                        
                                        case InputBinding.RadioChannelUp:

                                            if (!RadioHelper.IsSecondRadioAvailable())
                                            {
                                                RadioHelper.RadioChannelUp(1);
                                            }
                                            else
                                            {
                                                RadioHelper.RadioChannelUp(ClientStateSingleton.Instance.PlayerGameState.selected);
                                            }
                                          
                                            break;
                                        case InputBinding.RadioChannelDown:
                                            if (!RadioHelper.IsSecondRadioAvailable())
                                            {
                                                RadioHelper.RadioChannelDown(1);
                                            }
                                            else
                                            {
                                                RadioHelper.RadioChannelDown(ClientStateSingleton.Instance.PlayerGameState.selected);
                                            }
                                            break;
                                        case InputBinding.Radio1ChannelUp:
                                            RadioHelper.RadioChannelUp(1);
                                            break;
                                        case InputBinding.Radio1ChannelDown:
                                            RadioHelper.RadioChannelDown(1);
                                            break;
                                        case InputBinding.Radio2ChannelUp:
                                            RadioHelper.RadioChannelUp(2);
                                            break;
                                        case InputBinding.Radio2ChannelDown:
                                            RadioHelper.RadioChannelDown(2);
                                            break;
                                        case InputBinding.RadioChannel1:

                                            if (!RadioHelper.IsSecondRadioAvailable())
                                            {
                                                RadioHelper.SelectRadioChannel(1, 1);
                                            }
                                            else
                                            {
                                                RadioHelper.SelectRadioChannel(1, ClientStateSingleton.Instance.PlayerGameState.selected);
                                            }

                                            break;
                                        case InputBinding.RadioChannel2:
                                            if (!RadioHelper.IsSecondRadioAvailable())
                                            {
                                                RadioHelper.SelectRadioChannel(2, 1);
                                            }
                                            else
                                            {
                                                RadioHelper.SelectRadioChannel(2, ClientStateSingleton.Instance.PlayerGameState.selected);
                                            }
                                            break;
                                        case InputBinding.RadioChannel3:
                                            if (!RadioHelper.IsSecondRadioAvailable())
                                            {
                                                RadioHelper.SelectRadioChannel(3, 1);
                                            }
                                            else
                                            {
                                                RadioHelper.SelectRadioChannel(3, ClientStateSingleton.Instance.PlayerGameState.selected);
                                            }
                                            break;
                                        case InputBinding.RadioChannel4:
                                            if (!RadioHelper.IsSecondRadioAvailable())
                                            {
                                                RadioHelper.SelectRadioChannel(4, 1);
                                            }
                                            else
                                            {
                                                RadioHelper.SelectRadioChannel(4, ClientStateSingleton.Instance.PlayerGameState.selected);
                                            }
                                            break;
                                        case InputBinding.RadioChannel5:
                                            if (!RadioHelper.IsSecondRadioAvailable())
                                            {
                                                RadioHelper.SelectRadioChannel(5, 1);
                                            }
                                            else
                                            {
                                                RadioHelper.SelectRadioChannel(5, ClientStateSingleton.Instance.PlayerGameState.selected);
                                            }
                                            break;
                                        case InputBinding.RadioChannel6:
                                            if (!RadioHelper.IsSecondRadioAvailable())
                                            {
                                                RadioHelper.SelectRadioChannel(6, 1);
                                            }
                                            else
                                            {
                                                RadioHelper.SelectRadioChannel(6, ClientStateSingleton.Instance.PlayerGameState.selected);
                                            }
                                            break;
                                        case InputBinding.RadioChannel7:
                                            if (!RadioHelper.IsSecondRadioAvailable())
                                            {
                                                RadioHelper.SelectRadioChannel(7, 1);
                                            }
                                            else
                                            {
                                                RadioHelper.SelectRadioChannel(7, ClientStateSingleton.Instance.PlayerGameState.selected);
                                            }
                                            break;
                                        case InputBinding.RadioChannel8:
                                            if (!RadioHelper.IsSecondRadioAvailable())
                                            {
                                                RadioHelper.SelectRadioChannel(8, 1);
                                            }
                                            else
                                            {
                                                RadioHelper.SelectRadioChannel(8, ClientStateSingleton.Instance.PlayerGameState.selected);
                                            }
                                            break;
                                        case InputBinding.RadioChannel9:
                                            if (!RadioHelper.IsSecondRadioAvailable())
                                            {
                                                RadioHelper.SelectRadioChannel(9, 1);
                                            }
                                            else
                                            {
                                                RadioHelper.SelectRadioChannel(9, ClientStateSingleton.Instance.PlayerGameState.selected);
                                            }
                                            break;
                                        case InputBinding.RadioChannel10:
                                            if (!RadioHelper.IsSecondRadioAvailable())
                                            {
                                                RadioHelper.SelectRadioChannel(10, 1);
                                            }
                                            else
                                            {
                                                RadioHelper.SelectRadioChannel(10, ClientStateSingleton.Instance.PlayerGameState.selected);
                                            }
                                            break;
                                        case InputBinding.RadioChannel11:
                                            if (!RadioHelper.IsSecondRadioAvailable())
                                            {
                                                RadioHelper.SelectRadioChannel(11, 1);
                                            }
                                            else
                                            {
                                                RadioHelper.SelectRadioChannel(11, ClientStateSingleton.Instance.PlayerGameState.selected);
                                            }
                                            break;
                                        case InputBinding.RadioChannel12:
                                            if (!RadioHelper.IsSecondRadioAvailable())
                                            {
                                                RadioHelper.SelectRadioChannel(12, 1);
                                            }
                                            else
                                            {
                                                RadioHelper.SelectRadioChannel(12, ClientStateSingleton.Instance.PlayerGameState.selected);
                                            }
                                            break;
                                        case InputBinding.PreviousRadio:
                                            RadioHelper.PreviousRadio();
                                            break;
                                        case InputBinding.NextRadio:
                                            RadioHelper.NextRadio();
                                            break;
                                        case InputBinding.ReadStatus:
                                            RadioHelper.ReadStatus();
                                            break;
                                        case InputBinding.ToggleSelectedRadioMute:
                                            RadioHelper.ToggleSelectedRadioMute();
                                            break;
                                        case InputBinding.ToggleOtherRadioMute:
                                            RadioHelper.ToggleOtherRadioMute();
                                            break;
                                        case InputBinding.ToggleRadio1Mute:
                                            RadioHelper.ToggleRadio1Mute();
                                            break;
                                        case InputBinding.ToggleRadio2Mute:
                                            RadioHelper.ToggleRadio2Mute();
                                            break;
                                        case InputBinding.ToggleAllRadiosMute:
                                            RadioHelper.ToggleAllRadiosMute();
                                            break;
                                        case InputBinding.ToggleMicrophoneMute:
                                            RadioHelper.ToggleMicrophoneMute();
                                            break;
                                        case InputBinding.Radio1VolumeUp:
                                            RadioHelper.RadioVolumeUp(1);
                                            break;
                                        case InputBinding.Radio1VolumeDown:
                                            RadioHelper.RadioVolumeDown(1);
                                            break;
                                        case InputBinding.Radio2VolumeUp:
                                            RadioHelper.RadioVolumeUp(2);
                                            break;
                                        case InputBinding.Radio2VolumeDown:
                                            RadioHelper.RadioVolumeDown(2);
                                            break;
                                        case InputBinding.IntercomVolumeUp:
                                            RadioHelper.RadioVolumeUp(0);
                                            break;
                                        case InputBinding.IntercomVolumeDown:
                                            RadioHelper.RadioVolumeDown(0);
                                            break;

                                        default:
                                            break;
                                    }
                                }

                                break;
                            }
                        }
                    }

                    PollVolumeAxes(deviceStateCache);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "Exception in PTT input polling loop. Clearing PTT state and waiting for input recovery.");

                        try
                        {
                            callback(new List<InputBindState>());
                            MarkPttInputPoll();
                            WarnIfPttInputPollIsSlow(pollStopwatch.ElapsedMilliseconds, 0);
                        }
                        catch (Exception callbackEx)
                        {
                            Logger.Error(callbackEx, "Failed to publish cleared PTT state after input polling failure.");
                        }

                        _lastActiveBinding = InputBinding.ModifierIntercom;
                        _pilotRosterTogglePressed = false;
                        _clientListTogglePressed = false;
                        RequestDeviceReconnect();
                    }

                    Thread.Sleep(40);
                }
            });
            pttInputThread.IsBackground = true;
            pttInputThread.Start();
        }

        private void HandleWindowToggleBindings(List<InputBindState> bindStates)
        {
            var pilotRosterPressed = false;
            var clientListPressed = false;
            foreach (var bindState in bindStates)
            {
                if (bindState.MainDevice.InputBind == InputBinding.TogglePilotRoster)
                {
                    pilotRosterPressed = bindState.IsActive;
                }
                else if (bindState.MainDevice.InputBind == InputBinding.ToggleClientList)
                {
                    clientListPressed = bindState.IsActive;
                }
            }

            if (pilotRosterPressed && !_pilotRosterTogglePressed)
            {
                Application.Current.Dispatcher.BeginInvoke(_togglePilotRosterCallback);
            }
            if (clientListPressed && !_clientListTogglePressed)
            {
                Application.Current.Dispatcher.BeginInvoke(_toggleClientListCallback);
            }

            _pilotRosterTogglePressed = pilotRosterPressed;
            _clientListTogglePressed = clientListPressed;
        }


        private void SetAxisRange(Joystick joystick)
        {
            // Devices report axes in their own ranges; one range lets a volume axis read the same on any device.
            IList<DeviceObjectInstance> axes;
            try
            {
                axes = joystick.GetObjects(DeviceObjectTypeFlags.Axis);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Unable to list the axes of input device " + GetDeviceName(joystick));
                return;
            }

            foreach (var axis in axes)
            {
                try
                {
                    joystick.GetObjectPropertiesById(axis.ObjectId).Range =
                        new InputRange(VolumeAxis.RangeMin, VolumeAxis.RangeMax);
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, $"Unable to set the range of axis {NormalizeDeviceName(axis.Name)} on input device {GetDeviceName(joystick)}");
                }
            }
        }

        /// <summary>The detected InputDevice carries the axis index in Button.</summary>
        public void AssignAxis(DetectButton callback)
        {
            Task.Run(() =>
            {
                var joysticks = GetInputDeviceSnapshot()
                    .OfType<Joystick>()
                    .Where(joystick => !joystick.IsDisposed)
                    .ToList();
                var initial = new int[joysticks.Count][];
                var restarted = new bool[joysticks.Count];

                for (var i = 0; i < joysticks.Count; i++)
                {
                    initial[i] = ReadAxesForAssign(joysticks[i]);
                }

                while (true)
                {
                    Thread.Sleep(100);

                    for (var i = 0; i < joysticks.Count; i++)
                    {
                        if (initial[i] == null)
                        {
                            continue;
                        }

                        var current = ReadAxesForAssign(joysticks[i]);
                        if (current == null)
                        {
                            initial[i] = null;
                            continue;
                        }

                        var moved = VolumeAxis.FindMovedAxes(initial[i], current);
                        if (moved.Count == 0)
                        {
                            continue;
                        }

                        if (moved.Count > 1 && !restarted[i])
                        {
                            // Every axis jumps at once when a device sends its first report (see
                            // AxisReportWatch), so the first multi-axis change is a new starting point. A later one
                            // is a single control driving two axes.
                            initial[i] = current;
                            restarted[i] = true;
                            continue;
                        }

                        var information = joysticks[i].Information;
                        var inputDevice = new InputDevice
                        {
                            DeviceName = information.ProductName.Trim().Replace("\0", ""),
                            Button = moved[0],
                            InstanceGuid = information.InstanceGuid,
                            ProductGuid = information.ProductGuid,
                            ButtonValue = 0
                        };

                        Application.Current.Dispatcher.Invoke(() => { callback(inputDevice); });
                        return;
                    }
                }
            });
        }

        private int[] ReadAxesForAssign(Joystick joystick)
        {
            if (joystick == null || joystick.IsDisposed)
            {
                return null;
            }

            try
            {
                joystick.Poll();
                return VolumeAxis.ReadAxes(joystick.GetCurrentState());
            }
            catch (Exception e)
            {
                Logger.Error(e, $"Failed to get current state of input device {GetDeviceName(joystick)} " +
                    $"(ID: {GetProductGuid(joystick)}) while assigning an axis, ignoring it for this assignment");
                return null;
            }
        }

        private void PollVolumeAxes(Dictionary<Guid, object> deviceStateCache)
        {
            var currentInputProfile = _globalSettings.ProfileSettingsStore.GetCurrentInputProfile();

            foreach (var binding in VolumeAxis.Bindings)
            {
                InputDevice axisBinding;
                if (!currentInputProfile.TryGetValue(binding, out axisBinding) || axisBinding == null)
                {
                    _lastAxisVolumes.Remove(binding);
                    _lastAxisRadios.Remove(binding);
                    continue;
                }

                float volume;
                if (!TryGetAxisVolume(axisBinding, deviceStateCache, out volume))
                {
                    continue;
                }

                var radioId = VolumeAxis.GetRadioId(binding);
                var radio = RadioHelper.GetRadio(radioId);
                if (radio == null)
                {
                    continue;
                }

                // Applied only on movement, so the overlay slider still works in between. A new radio object
                // (new aircraft or reconnect) starts at 100%, so it gets the axis position at once.
                float lastVolume;
                RadioInformation lastRadio;
                if (_lastAxisVolumes.TryGetValue(binding, out lastVolume) &&
                    _lastAxisRadios.TryGetValue(binding, out lastRadio) &&
                    ReferenceEquals(lastRadio, radio) &&
                    !VolumeAxis.HasMoved(lastVolume, volume))
                {
                    continue;
                }

                // A disabled radio, or one whose volume the game sets, is retried on the next poll.
                if (RadioHelper.SetRadioVolume(volume, radioId))
                {
                    _lastAxisVolumes[binding] = volume;
                    _lastAxisRadios[binding] = radio;
                }
            }
        }

        private bool TryGetAxisVolume(InputDevice axisBinding, Dictionary<Guid, object> deviceStateCache, out float volume)
        {
            volume = 0;

            Guid instanceGuid;
            Device device;
            if (!TryGetInputDeviceForBinding(axisBinding, out instanceGuid, out device))
            {
                _missingBoundDevicesThisPoll.Add($"{NormalizeDeviceName(axisBinding.DeviceName)} ({axisBinding.InputBind})");
                RequestDeviceReconnect();
                return false;
            }

            var joystick = device as Joystick;
            if (joystick == null)
            {
                return false;
            }

            try
            {
                var state = GetDeviceState<JoystickState>(device, instanceGuid, deviceStateCache,
                    () =>
                    {
                        joystick.Poll();
                        return joystick.GetCurrentState();
                    });

                if (!_axisReportWatch.HasReported(device, VolumeAxis.ReadAxes(state)))
                {
                    return false;
                }

                volume = VolumeAxis.ToVolume(VolumeAxis.ReadAxis(state, axisBinding.Button),
                    VolumeAxis.IsInverted(axisBinding));
                return true;
            }
            catch (Exception e)
            {
                Logger.Error(e, $"Failed to get current state of input device {GetDeviceName(device)} " +
                    $"(ID: {GetProductGuid(device)}) while reading a volume axis. Removing device and waiting for automatic rediscovery.");

                RemoveInputDevice(instanceGuid, device);
                RequestDeviceReconnect();
                return false;
            }
        }

        public void StopPtt()
        {
            _detectPtt = false;
        }

        internal static bool IsRadioControlBinding(InputBinding binding)
        {
            return (binding >= InputBinding.RadioChannel1 && binding <= InputBinding.Radio2ChannelDown)
                   || binding == InputBinding.ToggleRadio1Mute
                   || binding == InputBinding.ToggleRadio2Mute
                   || (binding >= InputBinding.Radio1VolumeUp && binding <= InputBinding.IntercomVolumeDown);
        }

        private void MarkPttInputPoll()
        {
            Interlocked.Exchange(ref _lastPttInputPollUtcTicks, DateTime.UtcNow.Ticks);
        }

        private void WarnIfPttInputPollIsSlow(long elapsedMilliseconds, int bindingCount)
        {
            if (elapsedMilliseconds < SlowPttInputPollWarningMs)
            {
                return;
            }

            var now = DateTime.UtcNow.Ticks;
            if (new TimeSpan(now - Interlocked.Read(ref _lastSlowPttInputPollLogTicks)).TotalSeconds < 10)
            {
                return;
            }

            Interlocked.Exchange(ref _lastSlowPttInputPollLogTicks, now);
            var missing = _missingBoundDevicesThisPoll.Count > 0
                ? " Bound devices not found: " + string.Join(", ", _missingBoundDevicesThisPoll) + "."
                : string.Empty;
            Logger.Warn($"PTT input polling took {elapsedMilliseconds}ms across {bindingCount} configured bindings. " +
                        "Slow DirectInput devices can delay PTT and other keybinds." + missing);
        }

        private bool GetButtonState(InputDevice inputDeviceBinding, Dictionary<Guid, object> deviceStateCache)
        {
            Guid instanceGuid;
            Device device;
            if (!TryGetInputDeviceForBinding(inputDeviceBinding, out instanceGuid, out device))
            {
                bool recoveredButtonState;
                if (InputPollingCycleCache.ShouldAttemptRecoveryScan(false) &&
                    TryGetRecoveredActiveButtonState(inputDeviceBinding, Guid.Empty, out recoveredButtonState))
                {
                    return recoveredButtonState;
                }

                _missingBoundDevicesThisPoll.Add($"{NormalizeDeviceName(inputDeviceBinding.DeviceName)} ({inputDeviceBinding.InputBind})");
                RequestDeviceReconnect();
                return false;
            }

            try
            {
                return ReadButtonState(device, inputDeviceBinding, instanceGuid, deviceStateCache);
            }
            catch (Exception e)
            {
                Logger.Error(e, $"Failed to get current state of input device {GetDeviceName(device)} " +
                    $"(ID: {GetProductGuid(device)}) while retrieving button state. Removing device and waiting for automatic rediscovery.");

                RemoveInputDevice(instanceGuid, device);
                RequestDeviceReconnect();
            }

            return false;
        }

        private bool ReadButtonState(Device device, InputDevice inputDeviceBinding, Guid instanceGuid, Dictionary<Guid, object> deviceStateCache)
        {
            if (device is Joystick)
            {
                var state = GetDeviceState<JoystickState>(device, instanceGuid, deviceStateCache,
                    () =>
                    {
                        device.Poll();
                        return (device as Joystick).GetCurrentState();
                    });

                if (inputDeviceBinding.Button >= 128) //its a POV!
                {
                    var pov = state.PointOfViewControllers;
                    var povIndex = inputDeviceBinding.Button - 128;

                    return povIndex >= 0 &&
                           povIndex < pov.Length &&
                           pov[povIndex] == inputDeviceBinding.ButtonValue;
                }

                return inputDeviceBinding.Button >= 0 &&
                       inputDeviceBinding.Button < state.Buttons.Length &&
                       state.Buttons[inputDeviceBinding.Button];
            }

            if (device is Keyboard)
            {
                var keyboard = device as Keyboard;
                var state = GetDeviceState<KeyboardState>(device, instanceGuid, deviceStateCache,
                    () =>
                    {
                        keyboard.Poll();
                        return keyboard.GetCurrentState();
                    });
                return inputDeviceBinding.Button >= 0 &&
                       inputDeviceBinding.Button < state.AllKeys.Count &&
                       state.IsPressed(state.AllKeys[inputDeviceBinding.Button]);
            }

            if (device is Mouse)
            {
                var state = GetDeviceState<MouseState>(device, instanceGuid, deviceStateCache,
                    () =>
                    {
                        device.Poll();
                        return (device as Mouse).GetCurrentState();
                    });

                //just incase mouse changes number of buttons, like logitech can?
                return inputDeviceBinding.Button >= 0 &&
                       inputDeviceBinding.Button < state.Buttons.Length &&
                       state.Buttons[inputDeviceBinding.Button];
            }

            return false;
        }

        private TState GetDeviceState<TState>(Device device, Guid instanceGuid, Dictionary<Guid, object> deviceStateCache, Func<TState> pollState)
        {
            return InputPollingCycleCache.GetOrPoll(instanceGuid, deviceStateCache, pollState);
        }

        private bool TryGetRecoveredActiveButtonState(InputDevice inputDeviceBinding, Guid currentInstanceGuid, out bool buttonState)
        {
            foreach (var kpDevice in GetInputDeviceSnapshotWithKeys())
            {
                var device = kpDevice.Value;
                if (device == null ||
                    device.IsDisposed ||
                    kpDevice.Key.Equals(currentInstanceGuid) ||
                    !IsRecoveredInputDeviceMatch(inputDeviceBinding, device))
                {
                    continue;
                }

                try
                {
                    if (!ReadButtonState(device, inputDeviceBinding, kpDevice.Key, null))
                    {
                        continue;
                    }

                    Logger.Info($"Recovered active input binding {inputDeviceBinding.InputBind} from instance {inputDeviceBinding.InstanceGuid} " +
                                $"to {kpDevice.Key} ({GetDeviceName(device)})");

                    inputDeviceBinding.InstanceGuid = kpDevice.Key;

                    if (inputDeviceBinding.ProductGuid == Guid.Empty)
                    {
                        inputDeviceBinding.ProductGuid = GetProductGuid(device);
                    }

                    buttonState = true;
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Failed to poll recovered input device candidate {GetDeviceName(device)} " +
                        $"(ID: {GetProductGuid(device)}). Removing device and waiting for automatic rediscovery.");

                    RemoveInputDevice(kpDevice.Key, device);
                    RequestDeviceReconnect();
                }
            }

            buttonState = false;
            return false;
        }

        private bool TryGetInputDeviceForBinding(InputDevice inputDeviceBinding, out Guid instanceGuid, out Device device)
        {
            var deviceSnapshot = GetInputDeviceSnapshotWithKeys();

            foreach (var kpDevice in deviceSnapshot)
            {
                if (kpDevice.Value != null &&
                    !kpDevice.Value.IsDisposed &&
                    kpDevice.Key.Equals(inputDeviceBinding.InstanceGuid))
                {
                    instanceGuid = kpDevice.Key;
                    device = kpDevice.Value;
                    return true;
                }
            }

            var matchingDevices = deviceSnapshot
                .Where(kpDevice => kpDevice.Value != null &&
                                   !kpDevice.Value.IsDisposed &&
                                   IsRecoveredInputDeviceMatch(inputDeviceBinding, kpDevice.Value))
                .ToList();

            if (matchingDevices.Count == 1)
            {
                var recoveredDevice = matchingDevices[0];
                Logger.Info($"Recovered input binding {inputDeviceBinding.InputBind} from instance {inputDeviceBinding.InstanceGuid} " +
                            $"to {recoveredDevice.Key} ({GetDeviceName(recoveredDevice.Value)})");

                inputDeviceBinding.InstanceGuid = recoveredDevice.Key;

                if (inputDeviceBinding.ProductGuid == Guid.Empty)
                {
                    inputDeviceBinding.ProductGuid = GetProductGuid(recoveredDevice.Value);
                }

                instanceGuid = recoveredDevice.Key;
                device = recoveredDevice.Value;
                return true;
            }

            if (matchingDevices.Count > 1)
            {
                Logger.Warn($"Could not automatically recover input binding {inputDeviceBinding.InputBind} because multiple devices match " +
                            $"{inputDeviceBinding.DeviceName}. Reassign this binding if the device keeps reconnecting with a new instance ID.");
            }

            instanceGuid = Guid.Empty;
            device = null;
            return false;
        }

        private bool IsRecoveredInputDeviceMatch(InputDevice inputDeviceBinding, Device device)
        {
            var productGuid = GetProductGuid(device);
            if (inputDeviceBinding.ProductGuid != Guid.Empty && productGuid == inputDeviceBinding.ProductGuid)
            {
                return true;
            }

            var bindingDeviceName = NormalizeDeviceName(inputDeviceBinding.DeviceName);
            var currentDeviceName = NormalizeDeviceName(GetDeviceName(device));

            return bindingDeviceName.Length > 0 &&
                   string.Equals(bindingDeviceName, currentDeviceName, StringComparison.OrdinalIgnoreCase);
        }

        private string NormalizeDeviceName(string deviceName)
        {
            return (deviceName ?? string.Empty).Trim().Replace("\0", "");
        }

        private List<Device> GetInputDeviceSnapshot()
        {
            lock (_inputDevicesLock)
            {
                return _inputDevices.Values.ToList();
            }
        }

        private List<KeyValuePair<Guid, Device>> GetInputDeviceSnapshotWithKeys()
        {
            lock (_inputDevicesLock)
            {
                return _inputDevices.ToList();
            }
        }

        private void RemoveInputDevice(Guid instanceGuid, Device device)
        {
            lock (_inputDevicesLock)
            {
                Device currentDevice;
                if (!_inputDevices.TryGetValue(instanceGuid, out currentDevice) || !ReferenceEquals(currentDevice, device))
                {
                    return;
                }

                _inputDevices.Remove(instanceGuid);
            }

            try
            {
                device.Unacquire();
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Error unacquiring failed input device during reconnect handling");
            }

            try
            {
                device.Dispose();
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Error disposing failed input device during reconnect handling");
            }
        }

        private void RemoveInputDeviceLocked(Guid instanceGuid)
        {
            Device device;
            if (!_inputDevices.TryGetValue(instanceGuid, out device))
            {
                return;
            }

            _inputDevices.Remove(instanceGuid);

            try
            {
                device.Unacquire();
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Error unacquiring input device during forced reconnect refresh");
            }

            try
            {
                device.Dispose();
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Error disposing input device during forced reconnect refresh");
            }
        }

        private void RequestDeviceReconnect()
        {
            var now = DateTime.UtcNow;
            if (now < _nextDeviceReconnectAttemptUtc)
            {
                return;
            }

            _nextDeviceReconnectAttemptUtc = now.AddMilliseconds(_currentDeviceReconnectRetryMs);

            try
            {
                Logger.Info("Attempting automatic input device rediscovery");
                var changedDevices = InitDevicesInternal(false);
                if (changedDevices > 0)
                {
                    _currentDeviceReconnectRetryMs = MinDeviceReconnectRetryMs;
                    Logger.Info($"Automatic input device rediscovery added {changedDevices} device(s)");
                }
                else
                {
                    _currentDeviceReconnectRetryMs = Math.Min(_currentDeviceReconnectRetryMs * 2, MaxDeviceReconnectRetryMs);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Automatic input device rediscovery failed");
                _currentDeviceReconnectRetryMs = Math.Min(_currentDeviceReconnectRetryMs * 2, MaxDeviceReconnectRetryMs);
            }
        }

        private static DeviceIdentity ReadDeviceIdentity(Device device)
        {
            var information = device.Information;
            return new DeviceIdentity(information.ProductGuid, information.ProductName.Trim().Replace("\0", ""));
        }

        private string GetDeviceName(Device device)
        {
            try
            {
                return _deviceIdentities.Get(device).ProductName;
            }
            catch
            {
                return "unknown";
            }
        }

        private Guid GetProductGuid(Device device)
        {
            try
            {
                return _deviceIdentities.Get(device).ProductGuid;
            }
            catch
            {
                return Guid.Empty;
            }
        }

        public List<InputBindState> GenerateBindStateList()
        {
            var bindStates = new List<InputBindState>();
            var currentInputProfile = _globalSettings.ProfileSettingsStore.GetCurrentInputProfile();

            //REMEMBER TO UPDATE THIS WHEN NEW BINDINGS ARE ADDED
            //MIN + MAX bind numbers
            for (int i = (int)InputBinding.Intercom; i <= (int)InputBinding.IntercomVolumeDown; i++)
            {
                if (VolumeAxis.IsVolumeAxisBinding((InputBinding)i) || !currentInputProfile.ContainsKey((InputBinding)i))
                {
                    continue;
                }

                var input = currentInputProfile[(InputBinding)i];
                //construct InputBindState

                var bindState = new InputBindState()
                {
                    IsActive = false,
                    MainDevice = input,
                    MainDeviceState = false,
                    ModifierDevice = null,
                    ModifierState = false
                };

                if (currentInputProfile.ContainsKey((InputBinding)i + 100))
                {
                    bindState.ModifierDevice = currentInputProfile[(InputBinding)i + 100];
                }

                bindStates.Add(bindState);
            }

            return bindStates;
        }
    }
}
