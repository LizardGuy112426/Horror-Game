# Scene music

## Volume categories

- Music Sound / Music: scene BGM (including BirthdaySong and CG), ending music and chase music.
- Sound: UI click/hover, doors, player footsteps, jump/land, interaction and respawn effects.
- Enemy: monster voices/footsteps, jumpscares, spotting effects and heartbeat (including ChooseEnding).

Each category uses a sibling group in Resources/GameplayAudio. Assign future AudioSource
Output fields to the appropriate group. Do not use Master for category-specific sounds.
The three volume sliders use MusicVolumeSlider2D, SoundVolumeSlider2D and EnemyVolumeSlider2D;
their On Value Changed lists are intentionally empty because the components register listeners.
Values survive scene changes, but reset on a new game session. Source volumes remain authored
relative gains; fades and ducking do not overwrite the user's mixer volume.

## Scene configuration

Cutscene2 alone enables Cutscene Controller / Synchronize With Music. The scene stays
black while old BGM fades and the new clip loads, then music is scheduled 0.15 seconds
ahead on the DSP clock. Opening/closing fades, text, page transitions and image sequences
use absolute deadlines on that same clock; frame delays no longer accumulate per letter.
Missing music falls back to realtime with a warning (audio loading timeout: 10 seconds).
Leaving the scene cancels a pending scheduled start. Other CGs keep legacy playback.
Existing Inspector timing values remain authoritative: this does not stretch a CG to
match the full audio clip length. CG2's configured visual timeline is about 40.58 seconds.

Open a scene and select **Scene Music** in the Hierarchy. Its **Scene Music Cue 2D**
component exposes Music Clip, Loop, Fade Out Duration, Fade In Duration,
Restart On Enter and Volume. Save edits outside Play mode.

The runtime automatically loads `Assets/Resources/PersistentAudioSystem.prefab`.
Do not place extra copies in scenes. Scenes without a cue stop ordinary BGM;
gameplay SFX and nightmare chase music are separate sources.

Cutscene2 uses StartToHorror once at volume 0.847. An already playing track fades
out for one second before the shared start; direct entry only waits for audio preparation
and the short scheduling lead-in.
NM_Bedroom1 has no ordinary BGM. Leaving Cutscene2 stops that scene's music even
if the clip has not finished, in accordance with the existing CG timing.

Ending Animation and ChooseEnding retain their scene-authored audio and Timeline
bindings. The persistent BGM, ambient sounds and automatic chase music yield to
those scenes. Their scene-local Audio components do not destroy themselves or
try to persist a Timeline child object. Shared effect calls remain available.

**Audio Events** objects are local UnityEvent relays, not extra audio players.
They preserve menu/option-button audio callbacks after removing old Audio roots.
The original Audio prefab is retained for the ending's existing bindings.

Validation: `PersistentAudioValidation.Run` is a Unity batch execute method for
an isolated project copy. It checks cue references, scene transitions, the
one-second fade, chase startup, volume continuity and ending audio compatibility.
It does not replace a full listening test of the ending Timeline.
