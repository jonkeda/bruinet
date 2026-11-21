# Bruinet.Mocking.Sensors

Mock sensors for UI tests: `MockAudioSource`, `MockAudioSink`, `MockFrameSource` and `MockInputSource` stand in for a
device (a microphone, a speaker, a camera, a controller) so a test can feed an app what a device would.

**Name with it:** A UI suite whose app reads a device.

They were part of `Bruinet.Mocking` until that package became `Bruinet.Api.Mocking` (the WireMock server): a sensor
fakes a device, not a service. The namespace is unchanged: `Bruinet.Mocking.Sensors`.
