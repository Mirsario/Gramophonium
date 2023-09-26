# 🔥 What's this?
A silly C# library & CLI program for converting music & sounds to something sounding like it's coming out of an ancient gramophone.

Here's an input/output example, with default settings:

![Input](Demos/KevinMacLeod_MountainKing_Input.ogg)
![Output](Demos/KevinMacLeod_MountainKing_Output.ogg)

# 🤯 Why is this?
Videogames!

# ⚙️ How is this?
The library contains the following functions:
- Conversion of stereo audio to mono.
- High-pass filtering.
- Reduction of sampling rate.
- Most-basic volume normalization.

It operates on floating-point arrays and thus is format-agnostic.
But the CLI tool only works with OGG Vorbis.

# 📖 License
All code of this repository is provided under the MIT License.

The above track is:
> Hall of the Mountain King Kevin MacLeod (incompetech.com)
> Licensed under Creative Commons: By Attribution 3.0 License
> http://creativecommons.org/licenses/by/3.0/