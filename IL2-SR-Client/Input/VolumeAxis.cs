using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SharpDX.DirectInput;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.Settings
{
    /// <summary>
    /// Joystick axes bound to the radio volume sliders. The axis index is stored in InputDevice.Button
    /// and the invert flag in InputDevice.ButtonValue (1 = inverted).
    /// </summary>
    public static class VolumeAxis
    {
        public const int RangeMin = 0;
        public const int RangeMax = 65535;

        // Large enough that jitter on a resting axis is not taken for the axis being assigned.
        private const int DetectThreshold = (RangeMax - RangeMin) / 5;

        // Jitter on a resting axis stays below this, so it doesn't override a volume set with the mouse.
        private const float MovementThreshold = 0.01f;

        public static readonly IReadOnlyList<InputBinding> Bindings = new[]
        {
            InputBinding.Radio1VolumeAxis,
            InputBinding.Radio2VolumeAxis,
            InputBinding.IntercomVolumeAxis
        };

        // DIJOYSTATE2 holds four groups of 8 axes, and Windows puts the axes of a device with more than 8 into
        // the later groups. Bindings save the index, so the order must not change.
        private static readonly string[] AxisNames =
        {
            "X", "Y", "Z", "RX", "RY", "RZ", "Slider 1", "Slider 2",
            "VX", "VY", "VZ", "VRX", "VRY", "VRZ", "V Slider 1", "V Slider 2",
            "AX", "AY", "AZ", "ARX", "ARY", "ARZ", "A Slider 1", "A Slider 2",
            "FX", "FY", "FZ", "FRX", "FRY", "FRZ", "F Slider 1", "F Slider 2"
        };

        public static int AxisCount => AxisNames.Length;

        public static bool IsVolumeAxisBinding(InputBinding binding)
        {
            return Bindings.Contains(binding);
        }

        /// <summary>Index in PlayerGameState.radios, where 0 is the intercom.</summary>
        public static int GetRadioId(InputBinding binding)
        {
            switch (binding)
            {
                case InputBinding.Radio1VolumeAxis:
                    return 1;
                case InputBinding.Radio2VolumeAxis:
                    return 2;
                case InputBinding.IntercomVolumeAxis:
                    return 0;
                default:
                    throw new ArgumentOutOfRangeException(nameof(binding), binding, "Not a volume axis binding");
            }
        }

        public static string GetAxisName(int axis)
        {
            return axis >= 0 && axis < AxisNames.Length ? AxisNames[axis] : "?";
        }

        public static bool IsInverted(InputDevice device)
        {
            return device.ButtonValue == 1;
        }

        public static float ToVolume(int rawValue, bool inverted)
        {
            var volume = (float)(rawValue - RangeMin) / (RangeMax - RangeMin);
            volume = Math.Max(0f, Math.Min(1f, volume));
            return inverted ? 1f - volume : volume;
        }

        public static bool HasMoved(float previousVolume, float volume)
        {
            return Math.Abs(volume - previousVolume) >= MovementThreshold;
        }

        /// <summary>
        /// The axes that moved past the detect threshold, furthest first, so one control that drives two axes
        /// (a gamepad stick moved diagonally) resolves to the axis it mostly moved.
        /// </summary>
        public static List<int> FindMovedAxes(int[] initial, int[] current)
        {
            var count = Math.Min(initial.Length, current.Length);
            return Enumerable.Range(0, count)
                .Where(axis => Math.Abs(current[axis] - initial[axis]) >= DetectThreshold)
                .OrderByDescending(axis => Math.Abs(current[axis] - initial[axis]))
                .ToList();
        }

        /// <param name="readAxes">One reader per device; a reader returns null once its device can't be read.</param>
        public static bool TryWaitForMovedAxis(IReadOnlyList<Func<int[]>> readAxes, TimeSpan timeout, Action pause,
            out int device, out int axis)
        {
            var initial = readAxes.Select(read => read()).ToArray();
            var restarted = new bool[initial.Length];
            var waited = Stopwatch.StartNew();

            while (initial.Any(axes => axes != null) && waited.Elapsed < timeout)
            {
                pause();

                for (var i = 0; i < initial.Length; i++)
                {
                    if (initial[i] == null)
                    {
                        continue;
                    }

                    var current = readAxes[i]();
                    if (current == null)
                    {
                        initial[i] = null;
                        continue;
                    }

                    var moved = FindMovedAxes(initial[i], current);
                    if (moved.Count == 0)
                    {
                        continue;
                    }

                    if (moved.Count > 1 && !restarted[i])
                    {
                        // Every axis jumps at once when a device sends its first report (see AxisReportWatch),
                        // so the first multi-axis change is a new starting point. A later one is a single
                        // control driving two axes.
                        initial[i] = current;
                        restarted[i] = true;
                        continue;
                    }

                    device = i;
                    axis = moved[0];
                    return true;
                }
            }

            device = -1;
            axis = -1;
            return false;
        }

        public static int[] ReadAxes(JoystickState state)
        {
            var axes = new int[AxisCount];
            for (var i = 0; i < axes.Length; i++)
            {
                axes[i] = ReadAxis(state, i);
            }

            return axes;
        }

        public static int ReadAxis(JoystickState state, int axis)
        {
            var position = axis % 8;
            switch (axis / 8)
            {
                case 0:
                    return ReadGroup(position, state.X, state.Y, state.Z,
                        state.RotationX, state.RotationY, state.RotationZ, state.Sliders);
                case 1:
                    return ReadGroup(position, state.VelocityX, state.VelocityY, state.VelocityZ,
                        state.AngularVelocityX, state.AngularVelocityY, state.AngularVelocityZ, state.VelocitySliders);
                case 2:
                    return ReadGroup(position, state.AccelerationX, state.AccelerationY, state.AccelerationZ,
                        state.AngularAccelerationX, state.AngularAccelerationY, state.AngularAccelerationZ,
                        state.AccelerationSliders);
                case 3:
                    return ReadGroup(position, state.ForceX, state.ForceY, state.ForceZ,
                        state.TorqueX, state.TorqueY, state.TorqueZ, state.ForceSliders);
                default:
                    return RangeMin;
            }
        }

        private static int ReadGroup(int position, int x, int y, int z, int rx, int ry, int rz, int[] sliders)
        {
            switch (position)
            {
                case 0:
                    return x;
                case 1:
                    return y;
                case 2:
                    return z;
                case 3:
                    return rx;
                case 4:
                    return ry;
                case 5:
                    return rz;
                default:
                    var slider = position - 6;
                    return sliders != null && slider < sliders.Length ? sliders[slider] : RangeMin;
            }
        }
    }
}
