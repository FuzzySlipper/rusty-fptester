/** DOM companion: observes Engine projections and submits the reset intent. */
export function mountProductUi(root, context) {
  const panel = document.createElement('div');
  panel.className = 'fp-lab';
  panel.innerHTML = `
    <style>
      .fp-lab { position:fixed; inset:0; pointer-events:none; color:#ecf3fb; font:13px/1.5 system-ui,sans-serif; }
      .fp-lab * { box-sizing:border-box; }
      .fp-lab header { position:absolute; top:24px; left:28px; border-left:3px solid #58cab5; padding-left:14px; text-shadow:0 2px 4px #000; }
      .fp-lab h1 { margin:2px 0; font-size:22px; letter-spacing:.02em; font-weight:600; }
      .fp-lab .eyebrow { color:#8ed8cf; font:11px ui-monospace,monospace; letter-spacing:.14em; }
      .fp-lab .room { margin-top:8px; color:#c7d3e4; font-size:12px; }
      .fp-lab .telemetry { position:absolute; top:24px; right:24px; width:245px; padding:14px 18px; border:1px solid #ffffff26; border-radius:8px; background:#111a28d9; }
      .fp-lab .telemetry strong { color:#7adebc; font:11px ui-monospace,monospace; letter-spacing:.07em; }
      .fp-lab dl { display:grid; grid-template-columns:62px 1fr; margin:10px 0 0; gap:5px 12px; font:11px/1.6 ui-monospace,monospace; }
      .fp-lab dt { color:#93a5bd; } .fp-lab dd { margin:0; overflow-wrap:anywhere; }
      .fp-lab .reticle { position:absolute; left:50%; top:50%; width:5px; height:5px; border-radius:50%; background:#fff; box-shadow:0 0 0 2px #0007; transform:translate(-50%,-50%); }
      .fp-lab .target { position:absolute; top:calc(50% + 22px); left:50%; transform:translateX(-50%); background:#111a28bf; border-radius:5px; padding:4px 10px; white-space:nowrap; }
      .fp-lab .target:empty { display:none; }
      .fp-lab footer { position:absolute; bottom:24px; left:28px; right:28px; display:flex; align-items:flex-end; justify-content:space-between; gap:20px; }
      .fp-lab .guide { background:#111a28d9; border:1px solid #ffffff26; border-radius:8px; padding:12px 16px; }
      .fp-lab .guide p { margin:2px 0; } .fp-lab .muted { color:#a4b4c9; font-size:11px; }
      .fp-lab kbd { color:#fff; font:11px ui-monospace,monospace; background:#ffffff15; padding:1px 4px; border-radius:3px; }
      .fp-lab button { pointer-events:auto; cursor:pointer; color:#dfeafa; background:#203044; border:1px solid #667d96; border-radius:5px; padding:7px 12px; margin-top:8px; }
    </style>
    <header><div class="eyebrow">RUSTY ENGINE / CONTROLLER LAB</div><h1>First person playground</h1><div class="room" data-fact="room"></div></header>
    <aside class="telemetry" aria-label="Controller telemetry"><strong data-fact="grounded">CONNECTING</strong>
      <dl><dt>feet XYZ</dt><dd data-fact="position"></dd><dt>velocity</dt><dd data-fact="velocity"></dd><dt>stance</dt><dd data-fact="stance"></dd><dt>support</dt><dd data-fact="support"></dd><dt>contact</dt><dd data-fact="contact"></dd><dt>blocked</dt><dd data-fact="blocks"></dd><dt>step</dt><dd data-fact="step"></dd></dl>
    </aside>
    <div class="reticle"></div><output class="target" data-fact="target"></output>
    <footer><div class="guide"><p data-fact="message">Click the scene to capture the mouse</p><p><kbd>WASD</kbd> move · <kbd>Space</kbd> jump · <kbd>Shift</kbd> sprint · <kbd>Ctrl</kbd> crouch</p><p><kbd>E</kbd> pick up / drop · <kbd>Mouse</kbd> throw · <kbd>R</kbd> reset · <kbd>Esc</kbd> release mouse</p><p class="muted">Left: slopes 15° / 30° / 50° / 65°, steps 0.2–0.6m, crouch tunnel. Centre: jump course. Right: cubes & stack.</p><p class="muted">Bump-to-push unavailable in this Engine pair; cube contact blocking is available.</p></div>
      <div class="guide"><div class="muted" data-fact="actions"></div><button type="button" data-reset>Reset lab</button></div>
    </footer>`;
  root.append(panel);
  const fields = [...panel.querySelectorAll('[data-fact]')];
  const unsubscribe = context.projection.subscribe(envelope => {
    const facts = envelope?.value;
    if (!facts) return;
    for (const field of fields) field.textContent = facts[field.dataset.fact] ?? '';
  });
  const reset = panel.querySelector('[data-reset]');
  const onReset = () => context.intents.claim('reset', { kind:'digital', active:true });
  reset.addEventListener('click', onReset);
  return { dispose() { reset.removeEventListener('click',onReset); unsubscribe?.(); panel.remove(); } };
}
