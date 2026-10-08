DRON ESPADA JIAN - UNITY READY
================================

Archivos:
- dron_espada_unity.glb  <- recomendado
- dron_espada_unity.obj  <- respaldo
- materiales .mtl si aplica

Orientación:
- Blender/model: +Y frente, +Z arriba
- Para Unity, usa el GLB con glTFast o conviértelo a FBX en Blender.

Escala:
- Metros, 1.0
- Distancia entre motores diagonal opuestos: ~120.2 mm
- Hélices: 65 mm diámetro
- Motores: 8.5 mm diámetro x 20 mm largo

Nombres:
Body
Arms
Battery
Board
Motor_1_FL
Motor_2_FR
Motor_3_BR
Motor_4_BL
Prop_1_FL
Prop_2_FR
Prop_3_BR
Prop_4_BL
Guard_1_FL
Guard_2_FR
Guard_3_BR
Guard_4_BL

Pivots:
- Cada hélice tiene su origen local exactamente en el eje de su motor.
- Cada motor tiene su origen local en su propio centro.
- Body y Arms están centrados respecto al origen del dron.

Dimensiones aproximadas totales:
X = 0.1550 m (155.0 mm)
Y = 0.1550 m (155.0 mm)
Z = 0.0290 m (29.0 mm)

Nota:
GLB conserva mejor nodos, nombres, materiales y pivots que OBJ.
