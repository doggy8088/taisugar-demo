"""以 Blender 建立工傷介面的骨架參考模型；座標與既有人體模型一致。

執行：blender --background --python tools/build_skeleton.py
此為部位定位用的原創簡化模型，非醫療影像或診斷模型。
"""
from pathlib import Path
import math
import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[1]
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
material = bpy.data.materials.new('象牙骨質')
material.diffuse_color = (.79, .71, .55, 1)
material.use_nodes = True
material.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value = .62


def xyz(p):
    return Vector((p[0], -p[2], p[1]))


def finish(ob, name):
    ob.name = name
    ob.data.materials.clear()
    ob.data.materials.append(material)
    if ob.type == 'MESH':
        for face in ob.data.polygons:
            face.use_smooth = True
    return ob


def ellipsoid(name, center, scale):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=16, location=xyz(center))
    ob = bpy.context.object
    ob.scale = (scale[0], scale[2], scale[1])
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return finish(ob, name)


def tube(name, points, radius):
    curve = bpy.data.curves.new(name, 'CURVE')
    curve.dimensions = '3D'
    curve.resolution_u = 12
    curve.bevel_depth = radius
    curve.bevel_resolution = 3
    curve.use_fill_caps = True
    spline = curve.splines.new('BEZIER')
    spline.bezier_points.add(len(points) - 1)
    for node, point in zip(spline.bezier_points, points):
        node.co = xyz(point)
        node.handle_left_type = 'AUTO'
        node.handle_right_type = 'AUTO'
    ob = bpy.data.objects.new(name, curve)
    bpy.context.collection.objects.link(ob)
    bpy.context.view_layer.objects.active = ob
    ob.select_set(True)
    bpy.ops.object.convert(target='MESH')
    ob.select_set(False)
    return finish(ob, name)


def joined(name, objects):
    bpy.ops.object.select_all(action='DESELECT')
    for ob in objects:
        ob.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()
    ob = bpy.context.object
    ob.name = name
    ob.select_set(False)
    return ob


def long_bone(name, a, b, radius, head=1.55):
    a, b = Vector(a), Vector(b)
    axis = (b - a).normalized()
    tangent = axis.cross(Vector((0, 0, 1))).normalized()
    length = (b - a).length
    vertices, faces = [], []
    # 骨幹收窄、兩端加寬，保留不完全對稱的骨端。
    profile = [(0, .75), (.025, 1.3), (.07, head), (.15, 1.15), (.28, .8),
               (.5, .66), (.72, .76), (.87, 1.15), (.94, head), (.98, 1.25), (1, .7)]
    for t, width in profile:
        center = a.lerp(b, t) + tangent * (math.sin(t * math.pi) * length * .012)
        for i in range(16):
            angle = i * math.tau / 16
            point = center + tangent * (math.cos(angle) * radius * width) + axis.cross(tangent) * (math.sin(angle) * radius * width * .8)
            vertices.append(xyz(point))
    for j in range(len(profile) - 1):
        for i in range(16):
            k = j * 16 + i
            faces.append((k, j*16+(i+1)%16, (j+1)*16+(i+1)%16, k+16))
    faces += [tuple(reversed(range(16))), tuple(range(len(vertices)-16, len(vertices)))]
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    ob = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(ob)
    return finish(ob, name)


def subtract(ob, cutter):
    bpy.context.view_layer.objects.active = ob
    modifier = ob.modifiers.new('骨骼孔洞', 'BOOLEAN')
    modifier.operation = 'DIFFERENCE'
    modifier.solver = 'EXACT'
    modifier.object = cutter
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    bpy.data.objects.remove(cutter, do_unlink=True)


# 頭顱：顱腔外形、兩個眼眶、鼻孔、顴骨與獨立下顎。
cranium = ellipsoid('cranium', (0, 6.94, -.025), (.275, .34, .255))
face = ellipsoid('face-bones', (0, 6.715, .08), (.223, .21, .192))
bpy.context.view_layer.objects.active = cranium
union = cranium.modifiers.new('顱骨與顏面融合', 'BOOLEAN')
union.operation = 'UNION'
union.solver = 'EXACT'
union.object = face
bpy.ops.object.modifier_apply(modifier=union.name)
bpy.data.objects.remove(face, do_unlink=True)
for sign in [-1, 1]:
    subtract(cranium, ellipsoid('眼眶刀模', (sign*.112, 6.785, .247), (.089, .083, .17)))
subtract(cranium, ellipsoid('鼻孔刀模', (0, 6.657, .259), (.047, .079, .11)))
for sign in [-1, 1]:
    tube('zygomatic-'+str(sign), [(sign*.07,6.71,.21),(sign*.205,6.695,.14),(sign*.235,6.755,0)], .024)
mandible = tube('mandible', [(-.19,6.695,.005),(-.18,6.53,.115),(-.115,6.465,.19),(0,6.45,.215),(.115,6.465,.19),(.18,6.53,.115),(.19,6.695,.005)], .035)
for level in [6.545, 6.495]:
    for i in range(10):
        angle = -.96 + i * 1.92 / 9
        ellipsoid('tooth', (math.sin(angle)*.145, level, .08+math.cos(angle)*.135), (.023,.026,.023))

# 頸椎、胸椎與腰椎，依人體曲線排列並保留椎突。
for i in range(24):
    if i < 7:
        y = 6.38 - i*.074
        z = -.045 + .02*math.sin(i/6*math.pi)
        radius = .06
    elif i < 19:
        t = (i-7)/11
        y = 5.85 - t*1.00
        z = -.11-.105*math.sin(t*math.pi)
        radius = .072+t*.012
    else:
        t = (i-19)/4
        y = 4.73-t*.44
        z = -.12+.06*math.sin(t*math.pi)
        radius = .095
    parts = [ellipsoid('椎體',(0,y,z),(radius,.038,radius*.85)),
             tube('椎弓',[(-radius*.75,y,z-.02),(-radius*.7,y,z-.10),(0,y,z-.13),(radius*.7,y,z-.10),(radius*.75,y,z-.02)],.017),
             tube('棘突',[(0,y,z-.10),(0,y-.035,z-.20)],.019),
             tube('橫突',[(-radius*1.6,y,z-.08),(0,y,z-.07),(radius*1.6,y,z-.08)],.018)]
    joined(f'vertebra-{i+1}',parts)

# 肋骨由後方脊椎環繞胸腔至前方胸骨；最後兩對保留游離末端。
for i in range(12):
    t = i/11
    y = 5.83-t*.91
    width = .25+.32*math.sin((.13+t*.83)*math.pi)
    for sign, side in [(-1,'right'),(1,'left')]:
        points = [(sign*.075,y,-.16),(sign*width*.6,y+.015,-.245),
                  (sign*width,y-.035,-.075),(sign*width*.93,y-.10,.15)]
        if i < 10:
            points += [(sign*width*.52,y-.16,.258),(sign*(.06 if i<7 else .13),max(5.04,y-.17),.255)]
        else:
            points += [(sign*width*.70,y-.12,.13)]
        tube(f'{side}-rib-{i+1}',points,.020 if i<9 else .018)
joined('sternum', [ellipsoid('胸骨柄',(0,5.77,.265),(.085,.11,.034)),
                   long_bone('胸骨體',(0,5.68,.265),(0,5.16,.265),.045,1.0),
                   ellipsoid('劍突',(0,5.10,.25),(.025,.085,.021))])

# 骨盆：髂骨翼、坐骨與恥骨環，呈可辨識的盆狀外形。
for sign, side in [(-1,'right'),(1,'left')]:
    vertices, faces = [], []
    # 髂骨採上寬下窄的曲面，保留開放的髂嵴，而非封閉橢圓環。
    rows = [(4.49,.12,.48),(4.41,.14,.57),(4.25,.20,.57),(4.10,.29,.49),(4.01,.35,.41)]
    for y, inner, outer in rows:
        for column in range(7):
            u=column/6
            vertices.append(xyz((sign*(inner+(outer-inner)*u),y-.04*(1-u),-.15+.18*u-.10*math.sin(math.pi*u))))
    for row in range(4):
        for column in range(6):
            n=row*7+column
            faces.append((n,n+1,n+8,n+7))
    mesh=bpy.data.meshes.new(side+'-ilium');mesh.from_pydata(vertices,[],faces);mesh.update()
    wing=bpy.data.objects.new(side+'-ilium',mesh);bpy.context.collection.objects.link(wing)
    bpy.context.view_layer.objects.active=wing
    smooth=wing.modifiers.new('平滑髂骨翼','SUBSURF');smooth.levels=2
    bpy.ops.object.modifier_apply(modifier=smooth.name)
    thick=wing.modifiers.new('骨板厚度','SOLIDIFY');thick.thickness=.033
    bpy.ops.object.modifier_apply(modifier=thick.name)
    finish(wing,side+'-ilium')
    ring = tube('閉孔環',[(sign*.45,4.04,-.035),(sign*.33,3.84,.07),(sign*.13,3.85,.19),(sign*.10,4.04,.19),(sign*.30,4.10,.10),(sign*.45,4.04,-.035)],.040)
    joined(side+'-pelvis',[wing,ring])
joined('sacrum',[ellipsoid('薦骨',(0,4.09,-.16),(.14,.27,.075)),tube('尾椎',[(0,3.98,-.19),(0,3.82,-.11)],.032)])

for sign, side in [(-1,'right'),(1,'left')]:
    def p(x,y,z=0): return (sign*x,y,z)
    tube(side+'-clavicle',[p(.055,5.88,.23),p(.30,5.91,.17),p(.54,5.92,.05),p(.71,5.94,.025)],.033)
    # 肩胛骨的薄三角面與肩胛岡。
    vertices=[xyz(p(.18,5.83,-.23)),xyz(p(.63,5.86,-.18)),xyz(p(.37,5.29,-.24)),xyz(p(.22,5.53,-.29))]
    mesh=bpy.data.meshes.new(side+'-scapula');mesh.from_pydata(vertices,[],[(0,1,2,3)]);mesh.update()
    ob=bpy.data.objects.new(side+'-scapula',mesh);bpy.context.collection.objects.link(ob)
    thick=ob.modifiers.new('骨板厚度','SOLIDIFY');thick.thickness=.028
    bpy.context.view_layer.objects.active=ob;bpy.ops.object.modifier_apply(modifier=thick.name)
    bevel=ob.modifiers.new('骨板邊緣','BEVEL');bevel.width=.025;bevel.segments=3;bpy.ops.object.modifier_apply(modifier=bevel.name)
    finish(ob,side+'-scapula')
    tube(side+'-scapular-spine',[p(.20,5.7,-.28),p(.43,5.77,-.29),p(.67,5.90,-.12)],.032)
    long_bone(side+'-humerus',p(.74,5.91,.018),p(.978,5.09,.04),.065)
    ellipsoid(side+'-humeral-head',p(.728,5.91,.018),(.087,.095,.087))
    long_bone(side+'-radius',p(.946,5.025,.092),p(1.059,4.17,.123),.033)
    long_bone(side+'-ulna',p(1.009,5.035,-.025),p(1.125,4.17,.007),.032)
    # 手掌骨隨自然下垂姿勢排列，手指分節。
    fingers=[(.971,3.9337,.2552),(1.0199,3.6252,.2387),(1.0233,3.5863,.1053),(1.0433,3.622,.0055),(1.0342,3.6849,-.0745)]
    hand=[]
    for j in range(2):
        for k in range(4): hand.append(ellipsoid('腕骨',p(1.08+(j-.5)*.05,4.12-k*.017,.012+k*.037),(.024,.022,.023)))
    for i,(x,y,z) in enumerate(fingers):
        tip=Vector((x-.025,y-(.065 if i==0 else .11),z))
        knuckle=Vector((x+.045,y+(.07 if i==0 else .17),z))
        base=Vector((1.10,4.06,.05+(2-i)*.022))
        hand.append(long_bone('掌骨',p(*base),p(*knuckle),.020))
        count=2 if i==0 else 3
        for j in range(count):
            a=knuckle.lerp(tip,j/count);b=knuckle.lerp(tip,(j+1)/count)
            hand.append(long_bone('指骨',p(*a),p(*b),.015,1.23))
    joined(side+'-hand',hand)
    long_bone(side+'-femur',p(.47,3.95,-.005),p(.752,2.27,-.14),.086)
    ellipsoid(side+'-femoral-head',p(.40,4.01,.025),(.105,.103,.10))
    tube(side+'-femoral-neck',[p(.4,4.01,.025),p(.52,3.92,0)],.072)
    ellipsoid(side+'-patella',p(.757,2.235,.002),(.078,.095,.039))
    long_bone(side+'-tibia',p(.728,2.17,-.12),p(.724,.67,-.08),.062)
    long_bone(side+'-fibula',p(.838,2.14,-.15),p(.819,.62,-.10),.029)
    foot=[ellipsoid('距骨',p(.752,.62,-.07),(.078,.07,.083)),ellipsoid('跟骨',p(.755,.445,-.115),(.09,.08,.15))]
    toes=[(.5901,.4848,.4958),(.6922,.47,.5098),(.7648,.4569,.4981),(.8287,.4427,.4654),(.8893,.4311,.4495)]
    for i,(x,y,z) in enumerate(toes):
        base=Vector((.72+(i-2)*.035,.47,.10))
        knuckle=Vector((x,y,z-.11))
        foot.append(long_bone('蹠骨',p(*base),p(*knuckle),.022))
        count=2 if i==0 else 3
        for j in range(count):
            a=knuckle+Vector((0,-.005*j,.05*j))
            b=knuckle+Vector((0,-.005*(j+1),.05*(j+1)-.006))
            foot.append(long_bone('趾骨',p(*a),p(*b),.017 if i==0 else .013))
    joined(side+'-foot',foot)

bpy.ops.object.select_all(action='SELECT')
output=ROOT/'wwwroot/models/human/skeleton.glb'
bpy.ops.export_scene.gltf(filepath=str(output),export_format='GLB',export_animations=False,export_cameras=False,export_lights=False)
print('骨架已輸出',output,output.stat().st_size)
