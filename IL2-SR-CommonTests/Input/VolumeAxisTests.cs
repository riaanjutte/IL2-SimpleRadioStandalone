using System;
using System.Collections.Generic;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common.Tests.Input
{
    [TestClass]
    public class VolumeAxisTests
    {
        private static readonly int[] Resting = { 0, 0, 0, 0, 0, 0, 0, 0 };

        /// <summary>Returns each reading in turn, then keeps returning the last one.</summary>
        private static Func<int[]> Readings(params int[][] readings)
        {
            var next = 0;
            return () => readings[Math.Min(next++, readings.Length - 1)];
        }

        [TestMethod]
        public void AxisPositionMapsToVolume()
        {
            Assert.AreEqual(0f, VolumeAxis.ToVolume(VolumeAxis.RangeMin, false), 0.0001f);
            Assert.AreEqual(1f, VolumeAxis.ToVolume(VolumeAxis.RangeMax, false), 0.0001f);
            Assert.AreEqual(0.5f, VolumeAxis.ToVolume(VolumeAxis.RangeMax / 2, false), 0.001f);
        }

        [TestMethod]
        public void InvertedAxisMapsTheOtherWay()
        {
            Assert.AreEqual(1f, VolumeAxis.ToVolume(VolumeAxis.RangeMin, true), 0.0001f);
            Assert.AreEqual(0f, VolumeAxis.ToVolume(VolumeAxis.RangeMax, true), 0.0001f);
            Assert.AreEqual(0.75f, VolumeAxis.ToVolume(VolumeAxis.RangeMax / 4, true), 0.001f);
        }

        [TestMethod]
        public void OutOfRangeAxisValuesAreClamped()
        {
            Assert.AreEqual(0f, VolumeAxis.ToVolume(-500, false), 0.0001f);
            Assert.AreEqual(1f, VolumeAxis.ToVolume(VolumeAxis.RangeMax + 500, false), 0.0001f);
        }

        [TestMethod]
        public void SmallAxisJitterDoesNotCountAsMovement()
        {
            Assert.IsFalse(VolumeAxis.HasMoved(0.500f, 0.505f));
            Assert.IsTrue(VolumeAxis.HasMoved(0.50f, 0.52f));
            Assert.IsTrue(VolumeAxis.HasMoved(0.52f, 0.50f));
        }

        [TestMethod]
        public void AssigningPicksTheAxisThatMovedFarEnough()
        {
            var initial = new[] { 32767, 32767, 0, 0, 0, 0, 65535, 0 };
            var jitter = new[] { 32900, 32600, 100, 0, 0, 0, 65400, 0 };
            Assert.AreEqual(0, VolumeAxis.FindMovedAxes(initial, jitter).Count);

            var throttleMoved = new[] { 32900, 32600, 40000, 0, 0, 0, 65535, 0 };
            CollectionAssert.AreEqual(new[] { 2 }, VolumeAxis.FindMovedAxes(initial, throttleMoved));

            var sliderMoved = new[] { 32767, 32767, 0, 0, 0, 0, 10000, 0 };
            CollectionAssert.AreEqual(new[] { 6 }, VolumeAxis.FindMovedAxes(initial, sliderMoved));
        }

        [TestMethod]
        public void FirstReportJumpMovesSeveralAxesAtOnce()
        {
            var centred = new[] { 32768, 32768, 32768, 32768, 32768, 32768, 32768, 32768 };
            var firstReport = new[] { 0, 0, 65535, 32768, 10000, 32768, 32768, 50000 };
            Assert.IsTrue(VolumeAxis.FindMovedAxes(centred, firstReport).Count > 1);
        }

        [TestMethod]
        public void MovedAxesAreOrderedFurthestFirst()
        {
            // A gamepad stick pushed diagonally, mostly forwards.
            var initial = new[] { 32768, 32768, 0, 0, 0, 0, 0, 0 };
            var current = new[] { 50000, 0, 0, 0, 0, 0, 0, 0 };

            CollectionAssert.AreEqual(new[] { 1, 0 }, VolumeAxis.FindMovedAxes(initial, current));
        }

        [TestMethod]
        public void WaitingForAnAxisGivesUpWithoutJoysticks()
        {
            int device;
            int axis;
            Assert.IsFalse(VolumeAxis.TryWaitForMovedAxis(new List<Func<int[]>>(), TimeSpan.FromMinutes(1), () => { },
                out device, out axis));
        }

        [TestMethod]
        public void WaitingForAnAxisGivesUpWhenNoJoystickCanBeRead()
        {
            var readers = new List<Func<int[]>> { () => null, Readings(Resting, null) };

            int device;
            int axis;
            Assert.IsFalse(VolumeAxis.TryWaitForMovedAxis(readers, TimeSpan.FromMinutes(1), () => { },
                out device, out axis));
        }

        [TestMethod]
        public void WaitingForAnAxisGivesUpAfterTheTimeout()
        {
            var readers = new List<Func<int[]>> { Readings(Resting) };

            int device;
            int axis;
            Assert.IsFalse(VolumeAxis.TryWaitForMovedAxis(readers, TimeSpan.FromMilliseconds(50), () => { },
                out device, out axis));
        }

        [TestMethod]
        public void WaitingForAnAxisFindsTheDeviceAndAxisThatMoved()
        {
            var throttleMoved = new[] { 0, 0, 65535, 0, 0, 0, 0, 0 };
            var readers = new List<Func<int[]>> { Readings(Resting), Readings(Resting, Resting, throttleMoved) };

            int device;
            int axis;
            Assert.IsTrue(VolumeAxis.TryWaitForMovedAxis(readers, TimeSpan.FromMinutes(1), () => { },
                out device, out axis));
            Assert.AreEqual(1, device);
            Assert.AreEqual(2, axis);
        }

        [TestMethod]
        public void WaitingForAnAxisSkipsTheFirstReportJump()
        {
            var centred = new[] { 32768, 32768, 32768, 32768, 32768, 32768, 32768, 32768 };
            var firstReport = new[] { 0, 0, 65535, 32768, 10000, 32768, 32768, 50000 };
            var sliderMoved = new[] { 0, 0, 65535, 32768, 10000, 32768, 0, 50000 };
            var readers = new List<Func<int[]>> { Readings(centred, firstReport, sliderMoved) };

            int device;
            int axis;
            Assert.IsTrue(VolumeAxis.TryWaitForMovedAxis(readers, TimeSpan.FromMinutes(1), () => { },
                out device, out axis));
            Assert.AreEqual(6, axis);
        }

        [TestMethod]
        public void EachOverlayVolumeSliderHasAnAxisBinding()
        {
            Assert.AreEqual(1, VolumeAxis.GetRadioId(InputBinding.Radio1VolumeAxis));
            Assert.AreEqual(2, VolumeAxis.GetRadioId(InputBinding.Radio2VolumeAxis));
            Assert.AreEqual(0, VolumeAxis.GetRadioId(InputBinding.IntercomVolumeAxis));
            Assert.IsTrue(VolumeAxis.IsVolumeAxisBinding(InputBinding.IntercomVolumeAxis));
            Assert.IsFalse(VolumeAxis.IsVolumeAxisBinding(InputBinding.Ptt));
        }

        [TestMethod]
        public void VolumeAxesHaveNoModifierAndAreNotButtonControls()
        {
            foreach (var binding in VolumeAxis.Bindings)
            {
                Assert.IsFalse(Enum.IsDefined(typeof(InputBinding), (int)binding + 100), binding.ToString());
                Assert.IsFalse(InputDeviceManager.IsRadioControlBinding(binding), binding.ToString());
            }
        }

        [TestMethod]
        public void AxisNamesKeepTheirSavedIndexes()
        {
            Assert.AreEqual("X", VolumeAxis.GetAxisName(0));
            Assert.AreEqual("RZ", VolumeAxis.GetAxisName(5));
            Assert.AreEqual("Slider 2", VolumeAxis.GetAxisName(7));
            Assert.AreEqual("VX", VolumeAxis.GetAxisName(8));
            Assert.AreEqual(32, VolumeAxis.AxisCount);
            Assert.AreEqual("F Slider 2", VolumeAxis.GetAxisName(VolumeAxis.AxisCount - 1));
            Assert.AreEqual("?", VolumeAxis.GetAxisName(VolumeAxis.AxisCount));
        }
    }
}
