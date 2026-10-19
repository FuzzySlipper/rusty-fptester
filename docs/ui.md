# DOM companion

`src/ui/main.js` exports `mountProductUi`. It holds composition and focus only.

- **HUD:** observes the Engine-delivered `rusty.fptester.hud` projection.
- **Reset:** submits the declared `reset` semantic intent from the HUD button
  or the pause menu.
- **Pause menu (`M`):** uses the Engine lifecycle port through `pause.js`.
  The menu follows lifecycle state, so a pause from elsewhere also shows it.
  A reset claimed while paused reaches C# `HandlePausedIntents`.
- **Developer shell (`F3`, or the menu's entry):** docks the Engine developer
  shell beside running play, through `developer.js`. It needs a host started
  with `--live-debug`. It docks:
  - an inspection view framing the selected action's `reach`;
  - the tunables panel over the lab's page;
  - the scene tree;
  - the console.

  While open, it shows the `actions.volumes` overlay set. Gameplay input
  returns when it closes.

C# owns all scene, controller, action, world-time and interaction state. The
Engine owns:
- the canvas;
- input delivery and pointer lock;
- lifecycle;
- rendering and the camera;
- the debug panels;
- transport.

No gameplay loop or gameplay input listeners live in these modules. The
module-level key listener handles only the menu and shell keys.

Disposal removes the listeners, the projection subscription, the pause flow,
the shell and the panel.

Keep browser assets in `src/ui/` and documentation in `docs/`. Provenance is
in [reference.md](reference.md#product-ui-shell).
