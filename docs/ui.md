# DOM companion

`src/ui/main.js` exports `mountProductUi`. It observes the Engine-delivered
`rusty.fptester.hud` projection and submits the declared `reset` semantic intent.
C# owns all scene, controller and interaction state. The Engine owns the canvas,
input delivery, pointer lock, rendering, camera and transport.

The DOM displays controls, room names, controller telemetry and interaction
results. No gameplay loop or input listeners live in this module. Disposal
removes the reset listener, projection subscription and panel. Keep browser
assets in `src/ui/`, documentation in `docs/`.
