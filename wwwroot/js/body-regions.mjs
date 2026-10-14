export function pickBodyRegion(point, parts) {
    let closest;
    let distance = Infinity;
    for (const part of parts) {
        if (!part.width || (point.z < -.08 && part.surface === '正面') || (point.z > .08 && part.surface === '背面')) continue;
        const score = ((point.x - part.x) / Math.max(.09, part.width)) ** 2
            + ((point.y - part.y) / Math.max(.10, part.height)) ** 2
            + ((point.z - part.z) / Math.max(.18, part.depth)) ** 2;
        if (score < distance) { closest = part; distance = score; }
    }
    return closest;
}

// 依模型骨架校準位置，部位名稱與識別碼維持目錄定義。
export function fitBodyRegions(parts) {
    const positions = {
        shoulder: [.77, 5.94, .01], 'upper-arm': [.87, 5.48, .02],
        elbow: [.984, 5.066, .039], forearm: [1.05, 4.58, .06],
        wrist: [1.098, 4.12, .076], palm: [1.11, 3.91, .12], 'hand-back': [1.11, 3.91, .01],
        hip: [.53, 3.94, 0], thigh: [.62, 3.10, -.08], knee: [.757, 2.234, -.148],
        shin: [.757, 1.48, .02], calf: [.757, 1.48, -.25], ankle: [.757, .627, -.099],
        heel: [.757, .43, -.20], foot: [.757, .45, .20], sole: [.757, .33, .20]
    };
    positions['finger-1'] = [0.971, 3.9337, 0.2552];
    positions['toe-1'] = [0.5901, 0.4848, 0.4958];
    positions['finger-2'] = [1.0199, 3.6252, 0.2387];
    positions['toe-2'] = [0.6922, 0.47, 0.5098];
    positions['finger-3'] = [1.0233, 3.5863, 0.1053];
    positions['toe-3'] = [0.7648, 0.4569, 0.4981];
    positions['finger-4'] = [1.0433, 3.622, 0.0055];
    positions['toe-4'] = [0.8287, 0.4427, 0.4654];
    positions['finger-5'] = [1.0342, 3.6849, -0.0745];
    positions['toe-5'] = [0.8893, 0.4311, 0.4495];
    return parts.map(part => {
        const match = /^(left|right)-(.*)$/.exec(part.id);
        if (!match || !positions[match[2]]) return { ...part };
        const [x, y, z] = positions[match[2]];
        return { ...part, x: match[1] === 'left' ? x : -x, y, z };
    });
}
