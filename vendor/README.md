# PLib

Pinned PLib 4.21 binary used by both the mod and its integration verifier.

- Source: https://github.com/peterhaneve/ONIMods
- SHA-256: `b0144f909e00044ebd3b5880676ceb3aba756d579ea3a63aebd7a34b0777fda6`
- Code license: [MIT](PLib.LICENSE.txt), included in the built package.

Updating PLib is deliberate: build against the target ONI managed assemblies, run
`./scripts/check.sh`, then open, save and reopen the native options dialog in the game.
The build never selects an arbitrary installed mod's PLib copy.
