# Voice Splitter

Cuts each creature's raw recording (the loose WAVs in `Assets/Audio/Voices/`) into short lines of gibberish in their folder, ready for **Tools > Bush > Audio > Fill Sound Library From Folders**.

For each recording it:

1. Finds the syllables by level, ignoring the record-button clicks at either end.
2. Groups them into phrases, then builds 1 to 3 second lines: short phrases are joined with tight gaps, long ones are split at their widest pause. These are `<name>_01.wav` and so on.
3. Makes three remixes (`<name>_mix1.wav`...) by splicing bits from across the take, for variety.
4. Takes the room out of the whole take: the steady background hiss (learned from its quiet moments) and the short boxy ring after each sound. The voice itself is left alone.
5. Cleans each line: rumble cut, what's left of the room ducked between syllables, even loudness across every creature, a peak limiter and short fades. Gus and Pekapeka then get a small echo on purpose, which the room clean-up never touches.

The places to cut come from the untouched take, so the same recording always gives the same line names. Unity keeps their links when they're remade.

## Running it

Needs Python 3 with `numpy scipy soundfile`:

```bash
pip install numpy scipy soundfile
python Tools/VoiceSplitter/make_voices.py Assets/Audio/Voices Assets/Audio/Voices
python Tools/VoiceSplitter/check_voices.py Assets/Audio/Voices
```

The first argument is where the raw recordings are and the second is where the creature folders go. An optional third sets how hard the room clean-up works: `moderate` (the default), `strong` (drier, but voices can start to sound watery) or `off`. Moderate takes about 7 dB off the ring and 10 dB off the hiss on these recordings. Strong takes off another 3 or 4. It overwrites lines with the same names, so delete a folder's old clips first if the new run makes fewer. Which recording belongs to which creature, and who gets an echo, is the `CREATURES` table at the top of `make_voices.py`. Lengths and gaps are just below it.

`check_voices.py` lists each clip's length, peak and loudness. Anything starting or ending on a loud sound gets marked `<-- check`.
