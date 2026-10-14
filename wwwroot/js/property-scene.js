const root = document.querySelector('#property-scene');

if (root) {
    const assets = JSON.parse(document.querySelector('#property-scene-data').textContent);
    const byId = new Map(assets.map(asset => [asset.id, asset]));
    const unitAsset = new Map(assets.flatMap(asset => asset.units.map(unit => [unit.id, asset])));
    const state = { asset: null, floor: '', unit: null, search: '' };
    const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)');
    const search = root.querySelector('#scene-search');
    const floorSelect = root.querySelector('#scene-floor');
    const location = root.querySelector('[data-scene-location]');
    const title = root.querySelector('[data-inspector-title]');
    const eyebrow = root.querySelector('[data-inspector-eyebrow]');
    let engine;

    function matches(asset, unit = null) {
        if (!state.search) return true;
        const values = [asset.name, asset.code, asset.address];
        if (unit) values.push(unit.name, unit.code, unit.floor, unit.tenant);
        else asset.units.forEach(item => values.push(item.name, item.code, item.floor, item.tenant));
        return values.some(value => value.toLocaleLowerCase().includes(state.search));
    }

    function updateUI() {
        const asset = byId.get(state.asset);
        root.dataset.view = asset ? 'asset' : 'overview';
        location.textContent = asset ? asset.name : '';
        title.textContent = asset ? asset.name : `${assets.length} 處物業，一目瞭然。`;
        eyebrow.textContent = asset ? '樓層與出租單位' : '物業索引';
        root.querySelector('.scene-floor-field').hidden = !asset || asset.units.length === 0;
        root.querySelector('.scene-asset-list').hidden = Boolean(asset);
        let visibleCount = 0;
        root.querySelectorAll('[data-select-asset]').forEach(link => {
            const item = byId.get(Number(link.dataset.selectAsset));
            link.hidden = !matches(item);
            if (!asset && !link.hidden && link.classList.contains('scene-asset-card')) visibleCount++;
        });
        root.querySelectorAll('[data-asset-panel]').forEach(panel => {
            const item = byId.get(Number(panel.dataset.assetPanel));
            panel.hidden = item.id !== state.asset;
            panel.open = item.id === state.asset;
            panel.querySelectorAll('[data-floor]').forEach(group => {
                const matchingUnits = item.units.filter(unit => unit.floor === group.dataset.floor && matches(item, unit));
                group.hidden = (state.floor && group.dataset.floor !== state.floor) || !matchingUnits.length;
                group.querySelectorAll('[data-unit]').forEach(detail => {
                    const unit = item.units.find(value => value.id === Number(detail.dataset.unit));
                    detail.hidden = !matches(item, unit);
                    detail.open = state.unit === unit.id;
                    if (!panel.hidden && !group.hidden && !detail.hidden) visibleCount++;
                });
            });
        });
        root.querySelector('.scene-no-results').hidden = !state.search || visibleCount > 0;
    }

    function selectAsset(id) {
        state.asset = byId.has(id) ? id : null;
        state.floor = '';
        state.unit = null;
        state.search = '';
        search.value = '';
        const asset = byId.get(state.asset);
        floorSelect.replaceChildren(new Option('全部樓層', ''));
        if (asset) [...new Set(asset.units.map(unit => unit.floor))].forEach(floor => floorSelect.add(new Option(floor, floor)));
        updateUI();
        engine?.select(state);
    }

    function selectUnit(id) {
        const asset = unitAsset.get(id);
        if (!asset) return;
        if (state.asset !== asset.id) selectAsset(asset.id);
        state.unit = state.unit === id ? null : id;
        updateUI();
        engine?.highlight(state.unit);
        if (state.unit !== null) root.querySelector(`[data-unit="${id}"]`).scrollIntoView({ block: 'nearest', behavior: reducedMotion.matches ? 'instant' : 'smooth' });
    }

    function selectFloor(value) {
        state.floor = value;
        state.unit = null;
        floorSelect.value = value;
        updateUI();
        engine?.select(state);
    }

    root.addEventListener('click', event => {
        const asset = event.target.closest('[data-select-asset]');
        const unit = event.target.closest('[data-select-unit]');
        const floor = event.target.closest('[data-select-floor]');
        if (asset) { event.preventDefault(); selectAsset(Number(asset.dataset.selectAsset)); }
        else if (unit) { event.preventDefault(); selectUnit(Number(unit.dataset.selectUnit)); }
        else if (floor) selectFloor(floor.dataset.selectFloor);
        else if (event.target.closest('[data-scene-home]')) selectAsset(null);
        else if (event.target.closest('[data-scene-reset]')) engine?.select(state);
        else if (event.target.closest('[data-scene-zoom]')) engine?.zoom(event.target.closest('[data-scene-zoom]').dataset.sceneZoom === 'in' ? 1.2 : 1 / 1.2);
    });
    floorSelect.addEventListener('change', () => selectFloor(floorSelect.value));
    search.addEventListener('input', () => {
        state.search = search.value.trim().toLocaleLowerCase();
        updateUI();
    });
    root.dataset.enhanced = 'true';
    updateUI();

    function fallback(message = '目前使用清單檢視') {
        root.dataset.renderer = 'fallback';
        root.querySelector('.scene-loading strong').textContent = message;
        root.querySelector('.scene-loading > span:last-child').textContent = '物業、樓層與租約資料仍可從旁邊的清單操作。';
    }

    if (!assets.length) {
        fallback('從第一處物業開始');
    } else {
        root.querySelector('.scene-loading strong').textContent = '正在展開物業全景';
        Promise.all([import('three'), import('/vendor/three/OrbitControls.js')])
            .then(([THREE, { OrbitControls }]) => {
                engine = createScene(THREE, OrbitControls, assets, selectAsset, selectUnit, reducedMotion, fallback);
                root.dataset.renderer = 'ready';
                engine.select(state, true);
            }).catch(() => fallback());
    }
}

function createScene(THREE, OrbitControls, assets, selectAsset, selectUnit, reducedMotion, fallback) {
    const root = document.querySelector('#property-scene');
    const canvas = root.querySelector('#property-canvas');
    const viewport = root.querySelector('.scene-viewport');
    const labelLayer = root.querySelector('.scene-labels');
    const renderer = new THREE.WebGLRenderer({ canvas, antialias: true, alpha: true, powerPreference: 'low-power' });
    renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, window.innerWidth < 600 ? 1.25 : 1.75));
    renderer.shadowMap.enabled = true;
    renderer.shadowMap.type = THREE.PCFSoftShadowMap;
    renderer.toneMapping = THREE.ACESFilmicToneMapping;
    renderer.toneMappingExposure = 1.2;
    const scene = new THREE.Scene();
    const camera = new THREE.OrthographicCamera(-25, 25, 18, -18, 0.1, 180);
    camera.position.set(32, 31, 40);
    const controls = new OrbitControls(camera, canvas);
    controls.target.set(0, 3, 0);
    controls.enablePan = false;
    controls.enableDamping = false;
    controls.autoRotate = false;
    controls.minPolarAngle = Math.PI / 8;
    controls.maxPolarAngle = Math.PI / 2.5;
    controls.minZoom = 0.65;
    controls.maxZoom = 3;
    controls.update();
    scene.add(new THREE.HemisphereLight(0xfff9e7, 0x7d8b73, 2.7));
    const sun = new THREE.DirectionalLight(0xfff5dc, 3);
    sun.position.set(-18, 32, 14);
    sun.castShadow = true;
    sun.shadow.mapSize.set(1024, 1024);
    Object.assign(sun.shadow.camera, { left: -32, right: 32, top: 30, bottom: -30, near: 1, far: 90 });
    sun.shadow.normalBias = 0.06;
    scene.add(sun);
    const boxGeometry = new THREE.BoxGeometry(1, 1, 1);
    const sphereGeometry = new THREE.IcosahedronGeometry(1, 1);
    const materials = new Map();
    const pickables = [];
    const unitMaterials = new Map();
    const buildings = new Map();
    const labels = [];
    const colors = { '出租中': 0x66836c, '空置中': 0xb9c79b, '維修中': 0xc4a06e, '自用': 0x8d9fa7, '停用': 0xa6a69b };
    let animation;
    let frameId;
    let viewHeight = 31;
    let activeAsset = null;
    let activeFloor = '';
    let visible = true;
    let disposed = false;

    function material(color) {
        if (!materials.has(color)) materials.set(color, new THREE.MeshStandardMaterial({ color, roughness: .76, metalness: .05 }));
        return materials.get(color);
    }

    function box(parent, width, height, depth, x, y, z, surface) {
        const mesh = new THREE.Mesh(boxGeometry, typeof surface === 'number' ? material(surface) : surface);
        mesh.scale.set(width, height, depth);
        mesh.position.set(x, y, z);
        mesh.castShadow = true;
        mesh.receiveShadow = true;
        parent.add(mesh);
        return mesh;
    }

    function tree(parent, x, z, size = 1) {
        box(parent, .11, .7 * size, .11, x, .35 * size, z, 0x8c886c);
        const leaves = new THREE.Mesh(sphereGeometry, material(0x87996a));
        leaves.position.set(x, 1 * size, z);
        leaves.scale.set(.55 * size, .7 * size, .55 * size);
        leaves.castShadow = true;
        parent.add(leaves);
    }

    const site = new THREE.Group();
    scene.add(site);
    const columns = Math.ceil(Math.sqrt(assets.length * 1.3));
    const rows = Math.ceil(assets.length / columns);
    const siteWidth = columns * 10.5 + 2;
    const siteDepth = rows * 10 + 2;
    box(site, siteWidth, .5, siteDepth, 0, -.55, 0, 0xc7ceba);
    box(site, siteWidth - .25, .1, siteDepth - .25, 0, -.25, 0, 0xe0e3d5);
    for (let column = 1; column < columns; column++) {
        box(site, .9, .03, siteDepth - 1, (column - columns / 2) * 10.5, -.17, 0, 0xc2cbb7);
    }
    for (let row = 1; row < rows; row++) box(site, siteWidth - 1, .03, .9, 0, -.17, (row - rows / 2) * 10, 0xc2cbb7);
    const detailGround = new THREE.Group();
    box(detailGround, 9, .4, 8, 0, -.3, 0, 0xd1d8c2);
    box(detailGround, 8.8, .12, 7.8, 0, -.06, 0, 0xe2e6d6);
    tree(detailGround, -3.6, 2.8, .9);
    tree(detailGround, 3.5, -2.8, 1.1);
    detailGround.visible = false;
    scene.add(detailGround);

    function label(text, owner, height, data) {
        const element = document.createElement('button');
        element.type = 'button';
        element.className = 'scene-label';
        element.textContent = text;
        Object.assign(element.dataset, data);
        labelLayer.append(element);
        const item = { element, owner, position: new THREE.Vector3(0, height, 0), data };
        labels.push(item);
        return item;
    }

    assets.forEach((asset, index) => {
        const group = new THREE.Group();
        const original = new THREE.Vector3((index % columns - (columns - 1) / 2) * 10.5, 0, (Math.floor(index / columns) - (rows - 1) / 2) * 10);
        group.position.copy(original);
        scene.add(group);
        const floors = [...new Set(asset.units.map(unit => unit.floor))];
        const floorGroups = [];
        const plot = box(group, 8.3, .16, 7.8, 0, -.05, 0, 0xe9eadd);
        tree(group, -3.3, 2.8, .75);
        tree(group, 3.2, -2.7, .95);
        // 每片樓板對應實際資料中的樓層；同層單位依編號排列。
        floors.forEach((floor, floorIndex) => {
            const layer = new THREE.Group();
            layer.position.y = .3 + floorIndex * 1.2;
            group.add(layer);
            floorGroups.push({ layer, floor, overviewY: layer.position.y });
            const slab = box(layer, 5.6, .16, 4.7, 0, 0, 0, 0xe7e5d5);
            slab.userData = { assetId: asset.id };
            pickables.push(slab);
            const units = asset.units.filter(unit => unit.floor === floor);
            const cols = Math.min(units.length, 3);
            const rowCount = Math.ceil(units.length / cols);
            const width = 5.1 / cols;
            const depth = 4.2 / rowCount;
            units.forEach((unit, unitIndex) => {
                const x = (unitIndex % cols - (cols - 1) / 2) * width;
                const z = (Math.floor(unitIndex / cols) - (rowCount - 1) / 2) * depth;
                const glass = new THREE.MeshStandardMaterial({ color: colors[unit.status] ?? colors['停用'], roughness: .4, metalness: .15 });
                unitMaterials.set(unit.id, glass);
                const core = box(layer, width - .12, .88, depth - .12, x, .52, z, glass);
                core.userData = { assetId: asset.id, unitId: unit.id };
                pickables.push(core);
                const paneCount = Math.max(1, Math.floor(width / .8));
                for (let pane = 0; pane <= paneCount; pane++) {
                    const paneX = x - width / 2 + .09 + pane * (width - .18) / paneCount;
                    box(layer, .055, .94, depth - .05, paneX, .55, z, 0xdedfcf);
                }
                box(layer, width - .05, .07, depth - .05, x, .61, z, 0xdedfcf);
                if (unit.outstanding > 0 || unit.expiring) {
                    const flag = new THREE.Mesh(sphereGeometry, material(unit.outstanding > 0 ? 0xb8884c : 0xc9b878));
                    flag.scale.setScalar(.13);
                    flag.position.set(x + width * .3, 1.15, z + depth / 2);
                    layer.add(flag);
                }
            });
            label(floor, layer, 1.4, { selectFloor: floor, assetId: String(asset.id) });
        });
        const roof = box(group, 5.7, .17, 4.85, 0, .36 + Math.max(1, floors.length) * 1.2, 0, 0xd1d9c2);
        box(group, 1.8, .32, 1.2, .7, roof.position.y + .2, -.5, 0xe5e6d7);
        const roofObjects = group.children.filter(child => child !== plot && child !== roof && child.isMesh && child.position.y > roof.position.y);
        const assetLabel = label(asset.name, group, roof.position.y + 1.5, { selectAsset: String(asset.id) });
        buildings.set(asset.id, { group, original, floors: floorGroups, roof, roofObjects, assetLabel });
    });

    function updateLabels() {
        scene.updateMatrixWorld();
        const width = viewport.clientWidth, height = viewport.clientHeight;
        labels.forEach(item => {
            const show = item.data.selectAsset ? activeAsset === null : Number(item.data.assetId) === activeAsset && (!activeFloor || activeFloor === item.data.selectFloor);
            item.element.hidden = !show;
            if (!show) return;
            const projected = item.owner.localToWorld(item.position.clone()).project(camera);
            item.element.hidden = projected.z < -1 || projected.z > 1 || Math.abs(projected.x) > .96 || Math.abs(projected.y) > .94;
            item.element.style.left = `${(projected.x * .5 + .5) * width}px`;
            item.element.style.top = `${(-projected.y * .5 + .5) * height}px`;
        });
    }

    function draw(time) {
        frameId = undefined;
        if (disposed || !visible) return;
        if (animation) {
            const progress = Math.min(1, (time - animation.start) / animation.duration);
            const eased = 1 - Math.pow(1 - progress, 3);
            animation.moves.forEach(move => move.object.position.lerpVectors(move.from, move.to, eased));
            camera.position.lerpVectors(animation.cameraFrom, animation.cameraTo, eased);
            controls.target.lerpVectors(animation.targetFrom, animation.targetTo, eased);
            if (progress === 1) { animation = null; controls.enabled = true; }
            controls.update();
        }
        renderer.render(scene, camera);
        updateLabels();
        if (animation) requestRender();
    }

    function requestRender() {
        if (frameId === undefined && !disposed && visible) frameId = requestAnimationFrame(draw);
    }

    function resize() {
        const width = viewport.clientWidth, height = viewport.clientHeight;
        if (!width || !height) return;
        renderer.setSize(width, height, false);
        const aspect = width / height;
        camera.left = -viewHeight * aspect / 2;
        camera.right = viewHeight * aspect / 2;
        camera.top = viewHeight / 2;
        camera.bottom = -viewHeight / 2;
        camera.updateProjectionMatrix();
        requestRender();
    }

    function highlight(unitId) {
        unitMaterials.forEach((surface, id) => {
            surface.emissive.set(id === unitId ? 0xa0b982 : 0x000000);
            surface.emissiveIntensity = id === unitId ? .38 : 0;
        });
        requestRender();
    }

    function select(state, immediate = false) {
        activeAsset = state.asset;
        activeFloor = state.floor;
        site.visible = activeAsset === null;
        detailGround.visible = activeAsset !== null;
        const moves = [];
        let floorCount = 1;
        buildings.forEach((building, id) => {
            building.group.visible = activeAsset === null || activeAsset === id;
            if (!building.group.visible) return;
            const focused = activeAsset === id;
            moves.push({ object: building.group, from: building.group.position.clone(), to: focused ? new THREE.Vector3() : building.original });
            building.roof.visible = !focused;
            building.roofObjects.forEach(mesh => { mesh.visible = !focused; });
            building.floors.forEach((floor, index) => {
                floor.layer.visible = !focused || !activeFloor || activeFloor === floor.floor;
                const y = focused ? (activeFloor ? .3 : .3 + index * 2.6) : floor.overviewY;
                moves.push({ object: floor.layer, from: floor.layer.position.clone(), to: new THREE.Vector3(0, y, 0) });
            });
            if (focused) floorCount = activeFloor ? 1 : building.floors.length;
        });
        const targetHeight = activeAsset === null ? 3 : Math.max(1, floorCount * 1.15);
        viewHeight = activeAsset === null ? Math.max(25, rows * 10 + 7) : Math.max(13, floorCount * 2.7 + 5);
        // 窄畫面以完整物業範圍為準，避免總覽在左右被裁切。
        if (activeAsset === null) viewHeight = Math.max(viewHeight, (siteWidth + 9) / (viewport.clientWidth / viewport.clientHeight));
        camera.zoom = 1;
        resize();
        const targetTo = new THREE.Vector3(0, targetHeight, 0);
        const cameraTo = new THREE.Vector3(32, 28 + targetHeight, 40);
        highlight(state.unit);
        if (immediate || reducedMotion.matches) {
            animation = null;
            moves.forEach(move => move.object.position.copy(move.to));
            controls.target.copy(targetTo);
            camera.position.copy(cameraTo);
            controls.enabled = true;
            controls.update();
        } else {
            controls.enabled = false;
            animation = { start: performance.now(), duration: 480, moves, cameraFrom: camera.position.clone(), cameraTo, targetFrom: controls.target.clone(), targetTo };
        }
        requestRender();
    }

    let down;
    const pointer = new THREE.Vector2();
    const raycaster = new THREE.Raycaster();
    canvas.addEventListener('pointerdown', event => { down = { x: event.clientX, y: event.clientY }; });
    canvas.addEventListener('pointercancel', () => { down = null; });
    canvas.addEventListener('pointerup', event => {
        if (!down || Math.hypot(event.clientX - down.x, event.clientY - down.y) > 6) { down = null; return; }
        down = null;
        const rect = canvas.getBoundingClientRect();
        pointer.set((event.clientX - rect.left) / rect.width * 2 - 1, -((event.clientY - rect.top) / rect.height) * 2 + 1);
        raycaster.setFromCamera(pointer, camera);
        const isVisible = object => { for (let item = object; item; item = item.parent) if (!item.visible) return false; return true; };
        const hit = raycaster.intersectObjects(pickables, false).find(item => isVisible(item.object));
        if (!hit) return;
        const { assetId, unitId } = hit.object.userData;
        if (activeAsset === null) selectAsset(assetId);
        else if (unitId) selectUnit(unitId);
    });
    controls.addEventListener('change', requestRender);
    const resizeObserver = new ResizeObserver(resize);
    resizeObserver.observe(viewport);
    const visibilityObserver = new IntersectionObserver(entries => {
        visible = entries[0].isIntersecting;
        if (visible) requestRender();
    });
    visibilityObserver.observe(viewport);
    canvas.addEventListener('webglcontextlost', event => {
        event.preventDefault();
        disposed = true;
        cancelAnimationFrame(frameId);
        labelLayer.replaceChildren();
        fallback('3D 顯示暫時停止');
    });
    reducedMotion.addEventListener('change', () => {
        if (animation && reducedMotion.matches) {
            animation.moves.forEach(move => move.object.position.copy(move.to));
            camera.position.copy(animation.cameraTo);
            controls.target.copy(animation.targetTo);
            animation = null;
            controls.enabled = true;
            controls.update();
            requestRender();
        }
    });
    window.addEventListener('pagehide', event => {
        if (event.persisted) return;
        disposed = true;
        cancelAnimationFrame(frameId);
        resizeObserver.disconnect();
        visibilityObserver.disconnect();
        controls.dispose();
        boxGeometry.dispose();
        sphereGeometry.dispose();
        materials.forEach(surface => surface.dispose());
        unitMaterials.forEach(surface => surface.dispose());
        renderer.dispose();
    });
    resize();
    return { select, highlight, zoom: factor => {
        camera.zoom = Math.max(controls.minZoom, Math.min(controls.maxZoom, camera.zoom * factor));
        camera.updateProjectionMatrix();
        requestRender();
    } };
}
