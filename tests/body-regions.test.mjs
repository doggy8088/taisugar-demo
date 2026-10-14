import test from 'node:test';
import assert from 'node:assert/strict';
import { pickBodyRegion, fitBodyRegions } from '../wwwroot/js/body-regions.mjs';

const regions = [
    { id: 'left-shoulder', surface: '雙面', x: .87, y: 5.66, z: 0, width: .27, height: .28, depth: .29 },
    { id: 'right-shoulder', surface: '雙面', x: -.87, y: 5.66, z: 0, width: .27, height: .28, depth: .29 },
    { id: 'abdomen', surface: '正面', x: 0, y: 4.78, z: .10, width: .57, height: .43, depth: .33 },
    { id: 'lower-back', surface: '背面', x: 0, y: 4.78, z: -.20, width: .51, height: .43, depth: .19 },
    { id: 'other', surface: '其他', x: 0, y: 4, z: 0, width: 0, height: 0, depth: 0 }
];

test('人體表面點選能區分當事人左右肩', () => {
    assert.equal(pickBodyRegion({ x: .96, y: 5.65, z: .12 }, regions).id, 'left-shoulder');
    assert.equal(pickBodyRegion({ x: -.96, y: 5.65, z: .12 }, regions).id, 'right-shoulder');
});
test('腹部與下背共用高度時仍依身體正背面區分', () => {
    assert.equal(pickBodyRegion({ x: .1, y: 4.8, z: .35 }, regions).id, 'abdomen');
    assert.equal(pickBodyRegion({ x: .1, y: 4.8, z: -.35 }, regions).id, 'lower-back');
});
test('沒有圖面位置的其他部位不攔截點選，空清單安全回傳', () => {
    assert.equal(pickBodyRegion({ x: 0, y: 4, z: 0 }, [regions[4]]), undefined);
    assert.equal(pickBodyRegion({ x: 0, y: 4, z: 0 }, []), undefined);
});

 test('連續人體模型的手腕與膝蓋位置經校準，保留部位識別碼與左右對稱', () => {
    const fitted = fitBodyRegions([
        { id: 'left-wrist', x: 1.44, y: 3.61, z: 0, width: .13 },
        { id: 'right-knee', x: -.33, y: 2.43, z: .035, width: .205 }
    ]);
    assert.equal(fitted[0].id, 'left-wrist');
    assert.equal(fitted[0].x, 1.098);
    assert.equal(fitted[0].y, 4.12);
    assert.equal(fitted[1].x, -.757);
    assert.equal(fitted[1].y, 2.234);
});
