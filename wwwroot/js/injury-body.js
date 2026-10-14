const app = document.querySelector('#injury-app');

if (app) {
    const parts = JSON.parse(document.querySelector('#injury-body-data').textContent);
    const partMap = new Map(parts.map(part => [part.id, part]));
    const selected = new Set();
    const state = { group: '', side: '', search: '', view: 'front', skeleton: false, labels: true };
    const reduced = matchMedia('(prefers-reduced-motion: reduce)');
    const inputs = [...app.querySelectorAll('input[name="bodyParts"]')];
    let viewer;

    function syncSelection() {
        inputs.forEach(input => { input.checked = selected.has(input.value); });
        const region = app.querySelector('#body-selected-parts');
        region.replaceChildren();
        if (!selected.size) {
            const empty = document.createElement('p');
            empty.className = 'selection-empty';
            empty.textContent = '尚未選取部位。請點選人體或勾選右側清單。';
            region.append(empty);
        }
        parts.filter(part => selected.has(part.id)).forEach(part => {
            const chip = document.createElement('button');
            chip.type = 'button';
            chip.dataset.removePart = part.id;
            chip.setAttribute('aria-label', `移除${part.name}`);
            chip.textContent = part.name;
            const close = document.createElement('span');
            close.textContent = '×';
            close.setAttribute('aria-hidden', 'true');
            chip.append(close);
            region.append(chip);
        });
        app.querySelector('#body-selection-count').textContent = selected.size;
        app.querySelector('[data-body-clear]').disabled = !selected.size;
        app.querySelector('[data-body-confirm]').disabled = !selected.size;
        app.querySelector('#body-confirmed-summary').hidden = true;
        app.querySelector('#body-selection-status').textContent = selected.size ? `已標記 ${selected.size} 個部位。選取結果僅保留於本頁。` : '選取結果僅保留於本頁，尚未存入工傷個案。';
        viewer?.update();
    }

    function toggle(id, reveal = false) {
        if (!partMap.has(id)) return;
        if (selected.has(id)) selected.delete(id); else selected.add(id);
        syncSelection();
        if (reveal) {
            const row = app.querySelector(`[data-part-row="${id}"]`);
            row.closest('details').open = true;
            row.scrollIntoView({ block: 'nearest', behavior: reduced.matches ? 'instant' : 'smooth' });
        }
    }

    function filter() {
        let shown = 0;
        app.querySelectorAll('[data-body-group]').forEach(group => {
            let count = 0;
            group.querySelectorAll('[data-part-row]').forEach(row => {
                const part = partMap.get(row.dataset.partRow);
                row.hidden = Boolean((state.group && state.group !== part.group) || (state.side && state.side !== part.side) || (state.search && !`${part.name} ${part.group} ${part.side} ${part.surface}`.includes(state.search)));
                if (!row.hidden) count++;
            });
            group.hidden = count === 0;
            if (state.group || state.search || state.side) group.open = count > 0;
            shown += count;
        });
        app.querySelector('.body-no-results').hidden = shown > 0;
        viewer?.update();
    }

    app.addEventListener('change', event => {
        if (event.target.name === 'bodyParts') toggle(event.target.value);
        if (event.target.id === 'body-show-labels') { state.labels = event.target.checked; viewer?.update(); }
        if (event.target.id === 'body-group') { state.group = event.target.value; filter(); viewer?.focus(); }
    });
    app.querySelector('#body-search').addEventListener('input', event => { state.search = event.target.value.trim(); filter(); });
    app.addEventListener('click', event => {
        const control = event.target.closest('button');
        if (!control) return;
        if (control.dataset.removePart) toggle(control.dataset.removePart);
        else if (control.dataset.bodyPart) toggle(control.dataset.bodyPart, true);
        else if (control.hasAttribute('data-body-clear')) { selected.clear(); syncSelection(); }
        else if (control.hasAttribute('data-body-confirm')) {
            const output = app.querySelector('#body-confirmed-summary');
            output.textContent = `已確認 ${selected.size} 個部位：${parts.filter(part => selected.has(part.id)).map(part => part.name).join('、')}。`;
            output.hidden = false;
            app.querySelector('#body-selection-status').textContent = '部位已確認；本頁尚未建立或儲存工傷個案。';
        } else if (control.hasAttribute('data-body-side')) {
            state.side = control.dataset.bodySide;
            app.querySelectorAll('[data-body-side]').forEach(button => button.setAttribute('aria-pressed', String(button === control)));
            filter();
        } else if (control.dataset.bodyMode) {
            state.skeleton = control.dataset.bodyMode === 'skeleton';
            app.querySelectorAll('[data-body-mode]').forEach(button => button.setAttribute('aria-pressed', String(button === control)));
            viewer?.update();
        } else if (control.dataset.bodyView) {
            state.view = control.dataset.bodyView;
            app.querySelectorAll('[data-body-view]').forEach(button => button.setAttribute('aria-pressed', String(button === control)));
            viewer?.focus();
        } else if (control.dataset.bodyZoom) viewer?.zoom(control.dataset.bodyZoom === 'in' ? 1.2 : 1 / 1.2);
        else if (control.hasAttribute('data-body-reset')) {
            state.group = ''; state.view = 'front';
            app.querySelector('#body-group').value = '';
            app.querySelectorAll('[data-body-view]').forEach(button => button.setAttribute('aria-pressed', String(button.dataset.bodyView === 'front')));
            filter(); viewer?.focus();
        }
    });
    function fallback() {
        app.dataset.renderer = 'fallback';
        app.querySelector('.body-loading strong').textContent = '目前使用部位清單';
        app.querySelector('.body-loading p').textContent = '仍可勾選所有部位，並確認選取結果。';
    }
    Promise.all([import('three'), import('/vendor/three/OrbitControls.js')]).then(([THREE, { OrbitControls }]) => {
        viewer = renderBody(THREE, OrbitControls, parts, state, selected, id => toggle(id, true), reduced, fallback);
        app.dataset.renderer = 'ready';
        viewer.update();
    }).catch(fallback);
}

function renderBody(THREE, OrbitControls, parts, state, selected, toggle, reduced, fallback) {
    const app = document.querySelector('#injury-app');
    const stage = app.querySelector('.body-stage');
    const canvas = app.querySelector('#injury-canvas');
    const labelRoot = app.querySelector('.body-labels');
    const leaderRoot = app.querySelector('.body-leaders');
    const renderer = new THREE.WebGLRenderer({ canvas, alpha: true, antialias: true, powerPreference: 'low-power' });
    renderer.setPixelRatio(Math.min(devicePixelRatio || 1, 1.75));
    renderer.shadowMap.enabled = true;
    renderer.shadowMap.type = THREE.PCFSoftShadowMap;
    renderer.toneMapping = THREE.ACESFilmicToneMapping;
    renderer.toneMappingExposure = 1.2;
    const scene = new THREE.Scene();
    const camera = new THREE.OrthographicCamera(-4, 4, 4.2, -4.2, .1, 60);
    camera.position.set(0, 3.8, 12);
    const controls = new OrbitControls(camera, canvas);
    controls.target.set(0, 3.8, 0);
    controls.enablePan = false;
    controls.minPolarAngle = Math.PI / 3;
    controls.maxPolarAngle = Math.PI / 1.7;
    controls.minZoom = .7;
    controls.maxZoom = 4;
    controls.update();
    scene.add(new THREE.HemisphereLight(0xf8fff1, 0x8d9c91, 2.5));
    const key = new THREE.DirectionalLight(0xffffff, 3);
    key.position.set(-4, 9, 6);
    key.castShadow = true;
    key.shadow.mapSize.set(1024, 1024);
    Object.assign(key.shadow.camera, { left: -5, right: 5, top: 8, bottom: -4, near: .1, far: 25 });
    key.shadow.normalBias = .025;
    scene.add(key);
    const rim = new THREE.DirectionalLight(0xc3d7d0, 2);
    rim.position.set(3, 6, -5);scene.add(rim);
    const geometry = new THREE.SphereGeometry(1, 28, 20);
    const meshes = [];
    const partMeshes = new Map();
    const labels = [];
    const supportMeshes = [];
    const skin = new THREE.MeshStandardMaterial({ color: 0xd2ddd3, roughness: .6, transparent: true });
    let hoverId = null;
    let frame;
    let motion;
    let height = 8.35;
    let disposed = false;
    // 連續輪廓銜接關節，部位網格覆蓋其上並保留獨立選取。
    const torso = new THREE.Mesh(new THREE.LatheGeometry([
        new THREE.Vector2(.35, 3.86), new THREE.Vector2(.55, 4.08),
        new THREE.Vector2(.49, 4.50), new THREE.Vector2(.60, 5.05),
        new THREE.Vector2(.72, 5.52), new THREE.Vector2(.69, 5.76),
        new THREE.Vector2(.25, 5.99)
    ], 48), skin);
    torso.scale.z = .48;scene.add(torso);supportMeshes.push(torso);
    const headBase = new THREE.Mesh(new THREE.SphereGeometry(1, 32, 24), skin);
    headBase.position.set(0, 6.80, 0);headBase.scale.set(.35, .51, .33);scene.add(headBase);supportMeshes.push(headBase);
    function connector(a, b, radius) {
        const from = new THREE.Vector3(...a), to = new THREE.Vector3(...b), direction = to.clone().sub(from);
        const mesh = new THREE.Mesh(new THREE.CapsuleGeometry(radius, Math.max(.01, direction.length() - radius * 2), 6, 16), skin);
        mesh.position.copy(from.add(to).multiplyScalar(.5));
        mesh.quaternion.setFromUnitVectors(new THREE.Vector3(0, 1, 0), direction.normalize());
        scene.add(mesh);supportMeshes.push(mesh);
    }
    connector([0, 5.87, 0], [0, 6.42, 0], .19);
    for (const side of [-1, 1]) {
        connector([side * .87, 5.66, 0], [side * 1.18, 4.61, 0], .16);
        connector([side * 1.18, 4.61, 0], [side * 1.44, 3.61, 0], .12);
        connector([side * .33, 3.93, 0], [side * .33, 2.43, 0], .21);
        connector([side * .33, 2.43, 0], [side * .33, .85, 0], .14);
    }
    parts.forEach(part => {
        if (!part.width) return;
        const surface = new THREE.MeshStandardMaterial({ color: part.id.endsWith('-eye') ? 0x80998b : 0xd7e1d7, roughness: .47, metalness: .07, transparent: true, opacity: .98 });
        const mesh = new THREE.Mesh(geometry, surface);
        mesh.position.set(part.x, part.y, part.z);
        mesh.scale.set(part.width, part.height, part.depth);
        mesh.rotation.z = part.rotation;
        mesh.userData.part = part;
        mesh.castShadow = true;
        mesh.receiveShadow = true;
        scene.add(mesh);
        meshes.push(mesh);
        partMeshes.set(part.id, mesh);
    });
    const ground = new THREE.Mesh(new THREE.CircleGeometry(1.1, 64), new THREE.MeshStandardMaterial({ color: 0xdce5d4, roughness: 1, transparent: true, opacity: .65 }));
    ground.rotation.x = -Math.PI / 2;ground.position.y = .30;ground.receiveShadow = true;scene.add(ground);
    const skeleton = new THREE.Group();scene.add(skeleton);
    const boneMaterial = new THREE.MeshStandardMaterial({ color: 0xe9ddbb, roughness: .65 });
    function bone(a, b, radius = .06) {
        const from = new THREE.Vector3(...a), to = new THREE.Vector3(...b), delta = to.clone().sub(from);
        const mesh = new THREE.Mesh(new THREE.CylinderGeometry(radius, radius * .8, delta.length(), 10), boneMaterial);
        mesh.position.copy(from.add(to).multiplyScalar(.5));
        mesh.quaternion.setFromUnitVectors(new THREE.Vector3(0, 1, 0), delta.normalize());
        skeleton.add(mesh);
    }
    bone([0, 4, -.08], [0, 6.15, -.08], .095);
    for (const sign of [-1, 1]) {
        bone([0, 5.9, 0], [sign * .88, 5.67, 0]);
        bone([sign * .88, 5.67, 0], [sign * 1.18, 4.61, 0]);
        bone([sign * 1.18, 4.61, 0], [sign * 1.44, 3.61, 0], .045);
        bone([sign * .4, 4.0, 0], [sign * .33, 2.43, 0], .075);
        bone([sign * .33, 2.43, 0], [sign * .33, .9, 0], .06);
        bone([sign * .33, .9, 0], [sign * .33, .5, .4], .045);
    }
    for (let i = 0; i < 6; i++) {
        const ring = new THREE.Mesh(new THREE.TorusGeometry(.48 - i * .022, .025, 8, 40), boneMaterial);
        ring.rotation.x = Math.PI / 2;ring.scale.y = .6;ring.position.y = 5.77 - i * .14;skeleton.add(ring);
    }
    const skull = new THREE.Mesh(new THREE.SphereGeometry(.31, 20, 16), boneMaterial);
    skull.scale.y = 1.2;skull.position.set(0, 6.83, 0);skeleton.add(skull);
    skeleton.visible = false;

    function createLabels() {
        labelRoot.replaceChildren();leaderRoot.replaceChildren();labels.length = 0;
        const candidates = parts.filter(part => state.group ? part.group === state.group : part.overview || selected.has(part.id));
        candidates.filter(part => !state.side || state.side === part.side).forEach(part => {
            if (part.surface === '其他') return;
            const button = document.createElement('button');
            button.type = 'button';button.className = 'body-label';button.textContent = part.name;button.dataset.bodyPart = part.id;
            button.setAttribute('aria-pressed', String(selected.has(part.id)));
            labelRoot.append(button);
            const line = document.createElementNS('http://www.w3.org/2000/svg', 'line');
            const dot = document.createElementNS('http://www.w3.org/2000/svg', 'circle');dot.setAttribute('r', '2.3');
            leaderRoot.append(line, dot);
            labels.push({ part, button, line, dot });
        });
    }

    function positionLabels() {
        const w = stage.clientWidth, h = stage.clientHeight;
        const front = camera.position.z >= controls.target.z;
        const facingSide = Math.abs(camera.position.x) > Math.abs(camera.position.z) * 1.8;
        const lists = [[], []];
        labels.forEach((item, index) => {
            const p = item.part;
            const point = new THREE.Vector3(p.x, p.y, p.z).project(camera);
            const visible = state.labels && point.z > -1 && point.z < 1 && Math.abs(point.y) < .93 && (facingSide || p.surface !== (front ? '背面' : '正面'));
            item.button.hidden = !visible;item.line.style.display = visible ? '' : 'none';item.dot.style.display = visible ? '' : 'none';
            if (!visible) return;
            item.x = (point.x * .5 + .5) * w;item.y = (-point.y * .5 + .5) * h;
            const side = Math.abs(point.x) < .015 ? index % 2 : point.x > 0 ? 1 : 0;
            lists[side].push(item);
        });
        lists.forEach((list, side) => {
            list.sort((a, b) => a.y - b.y);
            const gap = Math.min(30, (h - 110) / Math.max(1, list.length));
            let previous = 50 - gap;
            list.forEach(item => { item.labelY = Math.max(item.y, previous + gap);previous = item.labelY; });
            const overflow = Math.max(0, previous - (h - 65));
            list.forEach(item => {
                const x = side ? w - 16 : 16;
                const y = Math.max(42, item.labelY - overflow);
                item.button.style.left = `${x}px`;item.button.style.top = `${y - 11}px`;
                item.button.style.transform = side ? 'translateX(-100%)' : '';
                const endX = side ? x - item.button.offsetWidth : x + item.button.offsetWidth;
                item.line.setAttribute('x1', item.x);item.line.setAttribute('y1', item.y);
                item.line.setAttribute('x2', endX);item.line.setAttribute('y2', y);
                item.dot.setAttribute('cx', item.x);item.dot.setAttribute('cy', item.y);
                if (selected.has(item.part.id)) { item.line.style.stroke = '#c78565';item.dot.style.fill = '#c78565'; }
            });
        });
        app.querySelector('.body-person-right').textContent = facingSide ? '' : front ? '當事人右側' : '當事人左側';
        app.querySelector('.body-person-left').textContent = facingSide ? '' : front ? '當事人左側' : '當事人右側';
    }

    function draw(now) {
        frame = undefined;if (disposed) return;
        if (motion) {
            const amount = Math.min(1, (now - motion.start) / 420), ease = 1 - Math.pow(1 - amount, 3);
            camera.position.lerpVectors(motion.from, motion.to, ease);controls.target.lerpVectors(motion.targetFrom, motion.targetTo, ease);controls.update();
            if (amount === 1) { motion = null;controls.enabled = true; }
        }
        renderer.render(scene, camera);positionLabels();
        if (motion) requestDraw();
    }
    function requestDraw() { if (frame === undefined && !disposed) frame = requestAnimationFrame(draw); }
    function resize() {
        const w = stage.clientWidth, h = stage.clientHeight;if (!w || !h) return;
        renderer.setSize(w, h, false);
        const aspect = w / h;
        camera.left = -height * aspect / 2;camera.right = height * aspect / 2;camera.top = height / 2;camera.bottom = -height / 2;
        camera.updateProjectionMatrix();requestDraw();
    }
    function update() {
        skeleton.visible = state.skeleton;
        skin.opacity = state.skeleton ? .10 : .96;
        skin.depthWrite = !state.skeleton;
        meshes.forEach(mesh => {
            const part = mesh.userData.part;
            const marked = selected.has(part.id);
            const filtered = (state.group && state.group !== part.group) || (state.side && state.side !== part.side);
            mesh.material.color.set(marked ? 0xc78468 : hoverId === part.id ? 0x9eb787 : part.id.endsWith('-eye') ? 0x80998b : 0xd7e1d7);
            mesh.material.opacity = marked ? .98 : state.skeleton ? .15 : filtered ? .20 : .98;
            mesh.material.depthWrite = !state.skeleton;
        });
        createLabels();requestDraw();
    }
    function focus() {
        const targets = state.group ? parts.filter(part => part.group === state.group && part.width) : parts.filter(part => part.width);
        const min = Math.min(...targets.map(part => part.y - part.height)), max = Math.max(...targets.map(part => part.y + part.height));
        const centerX = state.group ? targets.reduce((sum, part) => sum + part.x, 0) / Math.max(1, targets.length) : 0;
        const centerY = targets.length ? (min + max) / 2 : 3.8;
        height = state.group && targets.length ? Math.max(3.4, (max - min) * 1.25) : 8.35;
        const target = new THREE.Vector3(centerX, centerY, 0);
        const angle = { front: 0, back: Math.PI, left: Math.PI / 2, right: -Math.PI / 2 }[state.view];
        const position = new THREE.Vector3(centerX + Math.sin(angle) * 12, centerY, Math.cos(angle) * 12);
        camera.zoom = 1;resize();
        if (reduced.matches) { motion = null;camera.position.copy(position);controls.target.copy(target);controls.enabled = true;controls.update(); }
        else { motion = { start: performance.now(), from: camera.position.clone(), to: position, targetFrom: controls.target.clone(), targetTo: target };controls.enabled = false; }
        update();
    }
    const pointer = new THREE.Vector2(), raycaster = new THREE.Raycaster();let down;
    function hit(event) {
        const rect = canvas.getBoundingClientRect();pointer.set((event.clientX - rect.left) / rect.width * 2 - 1, -(event.clientY - rect.top) / rect.height * 2 + 1);
        raycaster.setFromCamera(pointer, camera);
        return raycaster.intersectObjects(meshes).find(item => (!state.group || item.object.userData.part.group === state.group) && (!state.side || item.object.userData.part.side === state.side))?.object.userData.part;
    }
    canvas.addEventListener('pointerdown', event => { down = [event.clientX, event.clientY]; });
    canvas.addEventListener('pointercancel', () => { down = null; });
    canvas.addEventListener('pointerup', event => {
        if (down && Math.hypot(event.clientX - down[0], event.clientY - down[1]) < 6) { const part = hit(event);if (part) toggle(part.id); }
        down = null;
    });
    canvas.addEventListener('pointermove', event => {
        if (down) return;
        const part = hit(event);
        canvas.style.cursor = part ? 'pointer' : 'grab';
        app.querySelector('.body-hover').textContent = part ? `${part.name} · 點選${selected.has(part.id) ? '取消標記' : '加入標記'}` : '將游標移至人體，查看部位名稱';
        if (hoverId !== part?.id) { hoverId = part?.id ?? null;update(); }
    });
    canvas.addEventListener('pointerleave', () => { hoverId = null;update(); });
    controls.addEventListener('change', requestDraw);
    const observer = new ResizeObserver(resize);observer.observe(stage);
    reduced.addEventListener('change', () => { if (reduced.matches && motion) focus(); });
    canvas.addEventListener('webglcontextlost', event => { event.preventDefault();disposed = true;cancelAnimationFrame(frame);labelRoot.replaceChildren();leaderRoot.replaceChildren();fallback(); });
    window.addEventListener('pagehide', event => {
        if (event.persisted) return;
        disposed = true;cancelAnimationFrame(frame);observer.disconnect();controls.dispose();
        meshes.forEach(mesh => mesh.material.dispose());geometry.dispose();
        supportMeshes.forEach(mesh => mesh.geometry.dispose());skin.dispose();
        skeleton.children.forEach(mesh => mesh.geometry.dispose());boneMaterial.dispose();ground.geometry.dispose();ground.material.dispose();renderer.dispose();
    });
    resize();
    return { update, focus, zoom(factor) { camera.zoom = Math.max(.7, Math.min(4, camera.zoom * factor));camera.updateProjectionMatrix();requestDraw(); } };
}
