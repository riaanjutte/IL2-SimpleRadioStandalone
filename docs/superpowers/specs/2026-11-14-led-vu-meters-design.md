# LED VU meters

## Why

Some players find the analogue dials too big and busy. They can choose realistic LED strip meters instead, and the
Microphone and Speakers panels shrink to fit them.

## What

- Settings › **VU Meter** gets a fourth choice, **LED**, next to Auto, Light and Dark (the dial faces). It switches
  live, like the others.
- Chosen look (mockup C): 16 rectangular segments in a black window with a glass reflection, and a dB scale printed
  underneath (−40, −20, −10, −6, 0, +6). Lit segments glow; unlit ones keep a dark tint of their colour.
- Segment thresholds (lit when the level is at or above): −40, −35, −30, −25, −20, −16, −13, −10, −8, −6 green;
  −4, −2, 0 amber; +2, +4, +6 red. Scale labels sit under the −40, −20, −10, −6, 0 and +6 segments.
- Peak hold: the highest segment reached stays lit for 1 second, then follows the level down.
- About 150 × 28 px, so the Microphone and Speakers panels compact around it (the dial is 150 × 80). The Audio Preview
  button and Speaker Boost slider move up under the strip.
- The LED colours are fixed (real LEDs), not taken from the theme.
- Classic is unchanged: it keeps its thin bars, and its VU Meter picker stays disabled.
- The window keeps its height; the freed space stays at the bottom of the General tab.

## Approaches

- **A small `LedVuMeter` control that draws its own segments (picked).** It sits beside each dial and reads the same
  dB value; choosing LED shows it instead of the dial. Peak hold is worked out from the time of each value change, so
  no timer, and the thresholds and peak hold are unit-testable.
- A second ProgressBar style in `MilitaryControls.xaml`: no new control, but peak hold needs remembered time, which a
  style can't hold, so code gets bolted on anyway.
- Reviving the pre-dial green bar from `c020951`: a continuous fill, so segments and peak hold mean rewriting it.

## Left to the builder

- Exact gradients, glow and spacing, matched to mockup C.
- How the control redraws (it only needs to redraw when the value changes; the client updates it continuously).
