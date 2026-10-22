# -*- coding: utf-8 -*-
"""
Генератор игровых данных для проекта «ВЫХОД».
Запуск:  python -I Tools/gen_data.py
Пишет JSON в Assets/Resources/Data/.  Любая таблица ниже — это контент игры:
добавил строку → в игре появилась пушка/мод/квест.
"""
import json, os, sys

OUT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'Assets', 'Resources', 'Data'))
os.makedirs(OUT, exist_ok=True)

def dump(name, data):
    p = os.path.join(OUT, name + '.json')
    with open(p, 'w', encoding='utf-8') as f:
        json.dump(data, f, ensure_ascii=False, indent=1)
    n = len(data) if isinstance(data, list) else sum(len(v) for v in data.values() if isinstance(v, list))
    print(f'{name}.json: {n} записей')

# ---------------------------------------------------------------- ТОРГОВЦЫ
TRADERS = [
    dict(id='mech',      name='Механик Семёныч', desc='Бывший оружейник завода. Оружие, модули, ремонт.', sells=['weapon','mod','ammo'], levels=[0, 0.15, 0.35, 0.6]),
    dict(id='doc',       name='Доктор Лена',     desc='Полевой хирург. Медицина и всё, что спасает жизнь.', sells=['med','food'], levels=[0, 0.1, 0.3, 0.55]),
    dict(id='fence',     name='Барыга Гоша',     desc='Скупает всё. Ключи, ценности, редкости.', sells=['valuable','key','barter'], levels=[0, 0.2, 0.4, 0.7]),
    dict(id='komendant', name='Комендант',       desc='Военный. Броня, шлемы, патроны и серьёзные задания.', sells=['armor','helmet','ammo','throwable'], levels=[0, 0.15, 0.35, 0.6]),
    dict(id='babulya',   name='Бабуля Зина',     desc='Держит лавку на рынке. Еда, вещи, простые просьбы.', sells=['food','backpack','rig','barter'], levels=[0, 0.1, 0.25, 0.5]),
    dict(id='hacker',    name='Хакер Ник',       desc='Электроника, разведданные, доступ к закрытым дверям.', sells=['electronics','key','mod'], levels=[0, 0.2, 0.45, 0.7]),
]

# ---------------------------------------------------------------- ПАТРОНЫ
# калибр: (урон, пробитие, скорость м/с, цена, дробин)
CALIBERS = {
    '9x18':    (38, 10, 315, 8, 1),  '9x19':   (46, 15, 380, 12, 1), '.45':     (58, 14, 290, 16, 1),
    '7.62x25': (54, 18, 430, 10, 1), '5.7x28': (36, 30, 620, 30, 1), '4.6x30':  (35, 32, 680, 32, 1),
    '.357':    (72, 20, 420, 25, 1), '12ga':   (24, 8, 380, 18, 8),  '20ga':    (20, 6, 360, 12, 7),
    '23x75':   (60, 12, 300, 40, 6), '5.45x39':(50, 25, 880, 20, 1), '5.56x45': (52, 27, 900, 22, 1),
    '7.62x39': (60, 30, 720, 24, 1), '7.62x51':(82, 42, 830, 45, 1), '7.62x54R':(85, 45, 860, 42, 1),
    '9x39':    (68, 35, 300, 40, 1), '.338':   (122, 60, 900, 120, 1),
}
AMMO_VARIANTS = [  # (суффикс, название, множ. урона, множ. пробития, множ. цены, редкость)
    ('std', 'обычный', 1.0, 1.0, 1.0, 1), ('ap', 'бронебойный', 0.9, 1.6, 2.2, 3),
    ('hp', 'экспансивный', 1.3, 0.45, 1.4, 2), ('tr', 'трассер', 0.95, 1.0, 1.1, 2),
]
ammo = []
for cal, (d, p, v, pr, pel) in CALIBERS.items():
    for suf, nm, dm, pm, cm, rar in AMMO_VARIANTS:
        if pel > 1 and suf in ('tr',):   # у дроби нет трассеров, есть пуля
            suf, nm, dm, pm, cm, rar, pel2 = 'slug', 'пуля', 3.2, 1.8, 1.3, 2, 1
        else:
            pel2 = pel
        ammo.append(dict(id=f'ammo_{cal}_{suf}', caliber=cal, name=f'{cal} {nm}', dmg=round(d*dm), pen=round(p*pm),
                         velocity=v, pellets=pel2, price=round(pr*cm), stack=60 if pel == 1 else 20, rarity=rar, weight=0.012))

# ---------------------------------------------------------------- ОРУЖИЕ
SLOTS = {
    'pistol':  ['muzzle', 'sight_p', 'light', 'mag'],
    'smg':     ['muzzle', 'sight', 'grip', 'stock', 'mag', 'light'],
    'ar':      ['muzzle', 'sight', 'grip', 'stock', 'mag', 'handguard', 'light', 'barrel'],
    'dmr':     ['muzzle', 'sight', 'stock', 'mag', 'bipod', 'grip'],
    'sniper':  ['muzzle', 'sight', 'stock', 'bipod'],
    'shotgun': ['muzzle', 'sight', 'stock', 'mag', 'light'],
    'lmg':     ['muzzle', 'sight', 'stock', 'mag', 'bipod', 'grip'],
    'melee':   [],
}
CLASS_NAMES = {'pistol':'Пистолет','smg':'Пистолет-пулемёт','ar':'Штурмовая винтовка','dmr':'Марксманская винтовка',
               'sniper':'Снайперская винтовка','shotgun':'Дробовик','lmg':'Пулемёт','melee':'Холодное оружие'}
# id, название, класс, калибр, семейство, скорострельность, режимы, отдача верт, отдача гориз, эргономика, точность(MOA), магазин, цена, уровень, торговец, модель(подсказка), ш, в, вес
W = [
 # ---- пистолеты
 ('pm',       'ПМ',               'pistol','9x18',   'pistol', 400,'s',   55, 30, 70, 4.5,  8,  9000, 1,'mech','pistol',      1,1,0.73),
 ('tt33',     'ТТ-33',            'pistol','7.62x25','pistol', 380,'s',   65, 35, 65, 4.0,  8, 11000, 1,'mech','pistol',      1,1,0.85),
 ('g17',      'Glock 17',         'pistol','9x19',   'pistol', 450,'s',   48, 26, 75, 3.5, 17, 24000, 2,'mech','pistol',      1,1,0.63),
 ('g18c',     'Glock 18C',        'pistol','9x19',   'pistol',1200,'sa',  70, 45, 70, 4.0, 17, 38000, 3,'mech','pistol',      1,1,0.68),
 ('p226',     'P226',             'pistol','9x19',   'pistol', 430,'s',   46, 25, 76, 3.2, 15, 27000, 2,'mech','pistol',      1,1,0.96),
 ('m1911',    'M1911A1',          'pistol','.45',    'pistol', 400,'s',   62, 32, 68, 3.8,  7, 25000, 2,'mech','pistol',      1,1,1.1),
 ('usp45',    'USP .45',          'pistol','.45',    'pistol', 420,'s',   58, 30, 72, 3.4, 12, 32000, 3,'mech','pistol',      1,1,0.9),
 ('fiveseven','Five-seveN',       'pistol','5.7x28', 'pistol', 450,'s',   40, 22, 78, 3.0, 20, 44000, 3,'mech','pistol',      1,1,0.62),
 ('fort12',   'Форт-12',          'pistol','9x18',   'pistol', 420,'s',   50, 28, 72, 3.9, 12, 14000, 1,'mech','pistol',      1,1,0.83),
 ('rev357',   'Револьвер .357',   'pistol','.357',   'pistol', 300,'s',   95, 50, 60, 3.0,  6, 30000, 2,'fence','revolver',   1,1,1.1),
 ('m9',       'Beretta M9',       'pistol','9x19',   'pistol', 440,'s',   47, 26, 74, 3.4, 15, 23000, 2,'mech','pistol',      1,1,0.95),
 # ---- ПП
 ('kedr',     'ПП-91 «Кедр»',     'smg','9x18',   'generic',  900,'sa',  60, 40, 72, 6.0, 30, 28000, 1,'mech','smg',         2,1,1.6),
 ('vityaz',   'ПП-19 «Витязь»',   'smg','9x19',   'ak',       700,'sa',  58, 36, 68, 5.0, 30, 42000, 2,'mech','smg',         3,1,2.9),
 ('mp5',      'MP5',              'smg','9x19',   'hk',       800,'sba', 50, 30, 70, 4.0, 30, 55000, 2,'mech','smg',         3,1,2.5),
 ('mp5k',     'MP5K',             'smg','9x19',   'hk',       900,'sa',  62, 40, 76, 5.5, 30, 48000, 2,'mech','smg',         2,1,2.0),
 ('ump45',    'UMP45',            'smg','.45',    'hk',       600,'sba', 64, 38, 66, 4.5, 25, 52000, 3,'mech','smg',         3,1,2.3),
 ('vector45', 'Vector .45',       'smg','.45',    'generic', 1100,'sba', 48, 28, 72, 4.2, 25, 95000, 4,'mech','smg',         3,1,2.7),
 ('mp7',      'MP7A1',            'smg','4.6x30', 'hk',       950,'sa',  45, 26, 80, 4.0, 40, 90000, 4,'mech','smg',         2,1,1.9),
 ('p90',      'P90',              'smg','5.7x28', 'generic',  900,'sa',  42, 24, 70, 3.6, 50, 98000, 4,'hacker','smg',       3,1,2.6),
 ('ppsh',     'ППШ-41',           'smg','7.62x25','generic', 1000,'sa',  75, 48, 55, 6.5, 71, 35000, 2,'babulya','smg',      3,1,3.6),
 ('uzi',      'Uzi',              'smg','9x19',   'generic',  600,'sa',  66, 44, 68, 6.0, 32, 33000, 2,'fence','smg',        2,1,3.5),
 ('scorpion', 'Scorpion EVO 3',   'smg','9x19',   'generic', 1150,'sba', 52, 32, 74, 4.4, 30, 70000, 3,'mech','smg',         3,1,2.8),
 ('bizon',    'ПП-19 «Бизон»',    'smg','9x18',   'ak',       680,'sa',  54, 34, 65, 5.2, 64, 46000, 3,'mech','smg',         3,1,2.7),
 # ---- дробовики
 ('mp133',    'МР-133',           'shotgun','12ga',  'generic', 60,'s',  120, 60, 55, 9.0,  6, 26000, 1,'mech','shotgun',     4,1,3.1),
 ('mp153',    'МР-153',           'shotgun','12ga',  'generic', 90,'s',  105, 55, 58, 8.5,  7, 38000, 2,'mech','shotgun',     4,1,3.2),
 ('saiga12',  'Сайга-12',         'shotgun','12ga',  'ak',     150,'s',  100, 50, 52, 9.0,  8, 60000, 3,'mech','shotgun',     4,1,3.6),
 ('toz106',   'ТОЗ-106',          'shotgun','20ga',  'generic', 45,'s',  110, 55, 62,10.0,  4, 12000, 1,'babulya','shotgun',  3,1,2.5),
 ('ks23',     'КС-23',            'shotgun','23x75', 'generic', 50,'s',  140, 70, 45,11.0,  3, 55000, 3,'komendant','shotgun',4,1,3.8),
 ('m870',     'Remington 870',    'shotgun','12ga',  'generic', 65,'s',  115, 58, 56, 8.8,  5, 32000, 2,'fence','shotgun',    4,1,3.3),
 ('m590',     'Mossberg 590',     'shotgun','12ga',  'generic', 65,'s',  112, 56, 58, 8.6,  8, 35000, 2,'mech','shotgun',     4,1,3.3),
 ('m4super',  'Benelli M4',       'shotgun','12ga',  'generic',180,'s',   98, 48, 60, 8.2,  7, 85000, 4,'komendant','shotgun',4,1,3.8),
 # ---- штурмовые
 ('ak74m',    'АК-74М',           'ar','5.45x39','ak',      650,'sa',  70, 40, 62, 3.0, 30, 48000, 2,'mech','rifle',         4,1,3.4),
 ('aks74u',   'АКС-74У',          'ar','5.45x39','ak',      650,'sa',  85, 50, 72, 4.5, 30, 32000, 1,'mech','rifle_short',   3,1,2.7),
 ('ak105',    'АК-105',           'ar','5.45x39','ak',      650,'sa',  76, 44, 68, 3.6, 30, 45000, 2,'mech','rifle',         4,1,3.2),
 ('ak12',     'АК-12',            'ar','5.45x39','ak',      700,'sba', 62, 36, 66, 2.8, 30, 72000, 3,'komendant','rifle',    4,1,3.5),
 ('ak74n',    'АК-74Н',           'ar','5.45x39','ak',      650,'sa',  72, 42, 60, 3.2, 30, 40000, 1,'mech','rifle',         4,1,3.3),
 ('akm',      'АКМ',              'ar','7.62x39','ak',      600,'sa',  95, 55, 60, 3.8, 30, 38000, 1,'mech','rifle',         4,1,3.3),
 ('akms',     'АКМС',             'ar','7.62x39','ak',      600,'sa',  98, 58, 58, 4.0, 30, 36000, 1,'babulya','rifle',      4,1,3.4),
 ('ak103',    'АК-103',           'ar','7.62x39','ak',      600,'sa',  90, 52, 62, 3.5, 30, 55000, 2,'mech','rifle',         4,1,3.4),
 ('ak104',    'АК-104',           'ar','7.62x39','ak',      600,'sa',  96, 56, 66, 4.0, 30, 52000, 2,'mech','rifle_short',   3,1,3.0),
 ('an94',     'АН-94 «Абакан»',   'ar','5.45x39','ak',      600,'sba', 55, 32, 55, 2.6, 30, 95000, 4,'komendant','rifle',    4,1,3.9),
 ('m4a1',     'M4A1',             'ar','5.56x45','ar15',    800,'sa',  60, 34, 70, 2.5, 30, 70000, 3,'mech','rifle',         4,1,3.0),
 ('hk416',    'HK416A5',          'ar','5.56x45','ar15',    850,'sa',  58, 32, 72, 2.4, 30, 98000, 4,'komendant','rifle',    4,1,3.4),
 ('scarl',    'SCAR-L',           'ar','5.56x45','scar',    625,'sa',  56, 30, 70, 2.5, 30, 90000, 4,'mech','rifle',         4,1,3.3),
 ('scarh',    'SCAR-H',           'ar','7.62x51','scar',    600,'sa',  88, 48, 62, 2.4, 20,120000, 4,'komendant','rifle',    4,1,3.6),
 ('adar',     'ADAR 2-15',        'ar','5.56x45','ar15',    800,'s',   64, 36, 66, 2.6, 30, 42000, 2,'mech','rifle',         4,1,3.1),
 ('aug',      'Steyr AUG A3',     'ar','5.56x45','generic', 700,'sa',  58, 34, 68, 2.4, 30, 85000, 3,'hacker','rifle',       3,1,3.6),
 ('g36',      'G36',              'ar','5.56x45','hk',      750,'sa',  60, 34, 70, 2.5, 30, 80000, 3,'mech','rifle',         4,1,3.6),
 ('fal',      'FN FAL',           'ar','7.62x51','generic', 650,'sa',  95, 52, 56, 2.6, 20, 95000, 4,'fence','rifle',        5,1,4.3),
 ('asval',    'АС «Вал»',         'ar','9x39',   'ak',      900,'sa',  66, 40, 70, 3.2, 20,110000, 4,'komendant','rifle',    4,1,2.5),
 ('sr3m',     'СР-3М',            'ar','9x39',   'ak',      900,'sa',  72, 44, 74, 3.6, 30,105000, 4,'hacker','rifle_short', 3,1,2.4),
 ('mdr',      'MDR .308',         'ar','7.62x51','generic', 700,'sa',  80, 44, 68, 2.3, 20,140000, 4,'hacker','rifle',       3,1,3.8),
 ('vpo136',   'ВПО-136 «Вепрь»',  'ar','7.62x39','ak',      600,'s',   98, 58, 58, 4.2, 10, 20000, 1,'babulya','rifle',      4,1,3.5),
 ('sks',      'СКС',              'dmr','7.62x39','generic', 400,'s',   85, 48, 60, 3.0, 10, 22000, 1,'babulya','rifle',     4,1,3.8),
 # ---- марксман
 ('svd',      'СВД',              'dmr','7.62x54R','generic', 300,'s', 120, 60, 50, 1.4, 10, 95000, 3,'komendant','sniper',  5,1,4.3),
 ('svds',     'СВДС',             'dmr','7.62x54R','generic', 300,'s', 125, 62, 54, 1.5, 10, 90000, 3,'mech','sniper',       4,1,4.6),
 ('vss',      'ВСС «Винторез»',   'dmr','9x39',    'ak',      800,'sa',  62, 36, 68, 2.4, 10,115000, 4,'komendant','sniper',  4,1,2.6),
 ('rsass',    'RSASS',            'dmr','7.62x51', 'ar15',    700,'s',   90, 48, 56, 1.2, 20,160000, 4,'hacker','sniper',     5,1,5.2),
 ('sr25',     'SR-25',            'dmr','7.62x51', 'ar15',    700,'s',   92, 50, 58, 1.3, 20,150000, 4,'mech','sniper',       5,1,4.9),
 ('m1a',      'M1A',              'dmr','7.62x51', 'generic', 650,'s',  100, 54, 52, 1.5, 20,110000, 3,'fence','sniper',      5,1,4.6),
 # ---- снайперские
 ('mosin',    'Мосина',           'sniper','7.62x54R','generic', 30,'s', 180, 80, 40, 1.2,  5, 25000, 1,'babulya','sniper',  5,1,4.0),
 ('sv98',     'СВ-98',            'sniper','7.62x54R','generic', 35,'s', 160, 70, 45, 0.8, 10, 95000, 3,'komendant','sniper', 5,1,6.2),
 ('m700',     'M700',             'sniper','7.62x51', 'generic', 35,'s', 150, 66, 48, 0.7,  5, 80000, 3,'mech','sniper',      5,1,4.1),
 ('t5000',    'Т-5000',           'sniper','7.62x51', 'generic', 32,'s', 140, 60, 44, 0.5, 10,170000, 4,'hacker','sniper',    5,1,6.0),
 ('dvl10',    'DVL-10',           'sniper','7.62x51', 'generic', 30,'s', 130, 56, 46, 0.6, 10,160000, 4,'komendant','sniper', 5,1,5.5),
 ('axmc',     'AXMC .338',        'sniper','.338',    'generic', 28,'s', 240,100, 36, 0.4, 10,320000, 5,'komendant','sniper', 6,1,7.5),
 ('vpo215',   'ВПО-215 «Горностай»','sniper','7.62x39','generic', 30,'s',130, 60, 50, 1.6,  4, 18000, 1,'babulya','sniper',  5,1,3.6),
 # ---- пулемёты
 ('rpk16',    'РПК-16',           'lmg','5.45x39', 'ak',      650,'sa',  80, 46, 50, 2.6, 95, 95000, 3,'komendant','rifle',   5,2,4.5),
 ('pkm',      'ПКМ',              'lmg','7.62x54R','generic', 650,'a',  130, 70, 36, 2.8,100,180000, 4,'komendant','rifle',   6,2,7.5),
 ('pkp',      'ПКП «Печенег»',    'lmg','7.62x54R','generic', 650,'a',  120, 66, 38, 2.5,100,210000, 5,'komendant','rifle',   6,2,8.2),
 ('m249',     'M249',             'lmg','5.56x45', 'generic', 850,'a',   90, 50, 40, 2.6,100,190000, 4,'hacker','rifle',      6,2,7.0),
 ('rpd',      'РПД',              'lmg','7.62x39', 'generic', 700,'a',  110, 60, 42, 3.0,100,120000, 3,'fence','rifle',       6,2,7.4),
 # ---- холодное
 ('knife',    'Нож',              'melee','',        'melee',  120,'s',   0, 0, 90, 0,   0,  3000, 1,'babulya','knife',       1,1,0.3),
 ('bayonet',  'Штык-нож 6Х5',     'melee','',        'melee',  110,'s',   0, 0, 88, 0,   0,  6000, 1,'komendant','knife',     1,1,0.4),
 ('machete',  'Мачете',           'melee','',        'melee',   90,'s',   0, 0, 80, 0,   0,  9000, 1,'fence','knife',         1,2,0.9),
 ('axe',      'Топор пожарный',   'melee','',        'melee',   70,'s',   0, 0, 70, 0,   0, 12000, 2,'babulya','axe',         1,3,2.2),
 ('crowbar',  'Монтировка',       'melee','',        'melee',   80,'s',   0, 0, 75, 0,   0,  7000, 1,'mech','crowbar',        1,3,1.5),
 ('sledge',   'Кувалда',          'melee','',        'melee',   45,'s',   0, 0, 55, 0,   0, 15000, 2,'mech','hammer',         1,3,4.5),
]
MELEE_DMG = {'knife':40,'bayonet':45,'machete':60,'axe':85,'crowbar':55,'sledge':120}
# Конкретная 3D-модель для каждого оружия (имя файла в Assets/Resources/Imported/*; см. Tools/fetch_assets.py):
#   guns_flat  — Pichuliru «Flat Guns East/West» (CC0): силуэты АК/СВД/Сайга/ПП и M4/SCAR/MP5/Benelli
#   guns_oga   — AK (текстурный), M4A1, Soviet_Assault_Rifle, Soviet_Special_Sniper_Rifle (ВСС), Quaternius FPS Pack, byzmod (CC0)
#   quaternius_guns — запасные вариации.  Второй элемент — вариант текстур (для AK: 0 чёрный, 1 деревянный).
WEAPON_MODELS = {
    # пистолеты
    'pm':'Pistol_Compact_East', 'tt33':'Pistol_Full_East', 'fort12':'Pistol_Compact_East',
    'g17':'Glock_HighPoly', 'g18c':'Glock_HighPoly', 'p226':'Pistol_Full_West', 'm1911':'byz_colt1911',
    'usp45':'Pistol_Compact_West', 'fiveseven':'Pistol_6', 'm9':'Pistol', 'rev357':'Revolver',
    # ПП
    'kedr':'SMG_Compact_East', 'vityaz':'SMG_Full_East', 'bizon':'SMG_Full_East', 'ppsh':'SubmachineGun_1',
    'mp5':'SMG_Full_West', 'mp5k':'SMG_Compact_West', 'ump45':'SubmachineGun_3', 'vector45':'SubmachineGun_2',
    'mp7':'SubmachineGun_5', 'p90':'P90', 'uzi':'SubmachineGun_4', 'scorpion':'SMG_Compact_West',
    # дробовики
    'mp133':'Shotgun_Pump_East', 'mp153':'Shotgun_Auto_East', 'saiga12':'Shotgun_Auto_East', 'toz106':'Shotgun_SawedOff',
    'ks23':'Shotgun_3', 'm870':'Shotgun_Pump_West', 'm590':'Shotgun_2', 'm4super':'Shotgun_Auto_West',
    # штурмовые (Rifle_Battle_East — короткий АК со складным прикладом → АКС-74У/АК-104/СР-3М)
    'ak74m':'Rifle_Assault_East', 'ak74n':'AssaultRifle_5', 'ak12':'Rifle_Assault_East', 'an94':'AssaultRifle_4',
    'aks74u':'Rifle_Battle_East', 'ak104':'Rifle_Battle_East', 'sr3m':'Rifle_Battle_East', 'ak105':'Soviet_Assault_Rifle',
    'akm':'AK', 'akms':'AssaultRifle_3', 'ak103':'AssaultRifle_4', 'vpo136':'AK',
    'm4a1':'M4A1', 'hk416':'Rifle_Assault_West', 'adar':'AssaultRifle2_1', 'scarl':'Rifle_Battle_West', 'scarh':'Rifle_Battle_West',
    'aug':'Bullpup_1', 'g36':'AssaultRifle2_3', 'fal':'AssaultRifle2_4', 'asval':'Soviet_Special_Sniper_Rifle', 'mdr':'Bullpup_2',
    'sks':'Rifle',
    # марксманские
    'svd':'Sniper_Material_East', 'svds':'Sniper_Material_East', 'vss':'Soviet_Special_Sniper_Rifle',
    'rsass':'Sniper_Rifle_West', 'sr25':'Sniper_Rifle_West', 'm1a':'byz_sniper',
    # снайперские
    'mosin':'byz_kar98k', 'sv98':'Sniper_Rifle_East', 'vpo215':'SniperRifle_6',
    'm700':'SniperRifle_1', 't5000':'Sniper_Material_West', 'dvl10':'Sniper_Material_West', 'axmc':'SniperRifle',
    # пулемёты (ленточных моделей в CC0-паках нет — длинные АК/AR-образные)
    'rpk16':'Rifle_Battle_East', 'pkm':'AssaultRifle_4', 'pkp':'AssaultRifle_4', 'm249':'AssaultRifle2_4', 'rpd':'AssaultRifle_3',
}
MODEL_VARIANT = {'akm':1, 'akms':1, 'vpo136':1}
weapons = []
for (wid, nm, cls, cal, fam, rpm, modes, rv, rh, ergo, moa, mag, price, lvl, trader, model, w, h, wt) in W:
    weapons.append(dict(id=wid, name=nm, cls=cls, clsName=CLASS_NAMES[cls], caliber=cal, family=fam, rpm=rpm,
                        modes=[{'s':'single','b':'burst','a':'auto'}[c] for c in modes], recoilV=rv, recoilH=rh,
                        ergo=ergo, moa=moa, magCap=mag, price=price, lvl=lvl, trader=trader,
                        model=WEAPON_MODELS.get(wid, model), modelClass=model, modelVariant=MODEL_VARIANT.get(wid, 0), w=w, h=h,
                        weight=wt, slots=SLOTS[cls], meleeDmg=MELEE_DMG.get(wid, 0), durability=100,
                        rarity={1:1,2:2,3:3,4:4,5:5}[lvl]))

# ---------------------------------------------------------------- МОДИФИКАЦИИ
mods = []
def mod(id, name, slot, stats, price, lvl, trader, cls=None, cal=None, fam=None, weapons_=None, model='', w=1, h=1, wt=0.2):
    mods.append(dict(id=id, name=name, slot=slot, stats=stats, price=price, lvl=lvl, trader=trader,
                     fits=dict(cls=cls or [], cal=cal or [], fam=fam or [], weapons=weapons_ or []),
                     model=model, w=w, h=h, weight=wt, rarity=min(5, max(1, lvl))))
CAL_GROUPS = {  # для дульных устройств/магазинов: группа калибров → список
    'pist9':  ['9x18','9x19','.357'], 'pist45': ['.45','7.62x25'], 'pdw': ['5.7x28','4.6x30'],
    'int545': ['5.45x39'], 'int556': ['5.56x45'], 'int762': ['7.62x39','9x39'], 'rif762': ['7.62x51','7.62x54R','.338'],
    'shot': ['12ga','20ga','23x75'],
}
CAL_NAMES = {'pist9':'9 мм','pist45':'.45/7.62x25','pdw':'PDW','int545':'5.45','int556':'5.56','int762':'7.62x39/9x39','rif762':'7.62x51/54R','shot':'12к'}
for g, cals in CAL_GROUPS.items():
    cn = CAL_NAMES[g]
    is_p = g.startswith('pist') or g == 'pdw'
    mod(f'brake_{g}_1', f'Дульный тормоз {cn} «Шторм»', 'muzzle', dict(recoil=-0.08, ergo=-2), 9000, 1, 'mech', cal=cals, model='muzzle')
    mod(f'brake_{g}_2', f'Компенсатор {cn} «Титан»',   'muzzle', dict(recoil=-0.14, ergo=-4), 24000, 3, 'mech', cal=cals, model='muzzle')
    mod(f'flash_{g}',   f'Пламегаситель {cn}',         'muzzle', dict(recoil=-0.03, flash=0.5), 6000, 1, 'komendant', cal=cals, model='muzzle')
    mod(f'supp_{g}_1',  f'Глушитель {cn} «Шёпот»',     'muzzle', dict(recoil=-0.06, ergo=-8, suppressed=1, loud=-0.6, velocity=0.02), 40000, 2, 'mech', cal=cals, model='suppressor', w=2, wt=0.5)
    mod(f'supp_{g}_2',  f'Глушитель {cn} «Тишина Pro»','muzzle', dict(recoil=-0.11, ergo=-5, suppressed=1, loud=-0.75, velocity=0.03, moa=-0.05), 95000, 4, 'hacker', cal=cals, model='suppressor', w=2, wt=0.45)
    if not is_p:
        mod(f'mag_{g}_ext', f'Магазин увеличенный {cn}', 'mag', dict(capacity=15 if g != 'shot' else 4, ergo=-6, weight=0.3), 14000, 2, 'mech', cal=cals, model='mag')
        mod(f'mag_{g}_drum', f'Барабанный магазин {cn}', 'mag', dict(capacity=45 if g != 'shot' else 12, ergo=-18, weight=0.9), 55000, 4, 'komendant', cal=cals, model='mag', w=2, h=2)
        mod(f'mag_{g}_quick', f'Магазин облегчённый {cn}', 'mag', dict(capacity=0, ergo=4, reload=-0.12), 18000, 3, 'mech', cal=cals, model='mag')
    else:
        mod(f'mag_{g}_ext', f'Магазин удлинённый {cn}', 'mag', dict(capacity=8, ergo=-4), 9000, 2, 'mech', cal=cals, model='mag')
# прицелы
SIGHTS = [  # id, name, zoom, ergo, price, lvl, trader, slot
    ('rds_pk06','Коллиматор ПК-06',1.0,-2,12000,1,'mech'),('rds_eotech','Голографический EXPS3',1.0,-3,38000,3,'hacker'),
    ('rds_aim','Коллиматор Aimpoint T1',1.0,-1,30000,2,'mech'),('rds_okp','ОКП-7',1.0,-3,15000,1,'komendant'),
    ('rds_romeo','Romeo 4',1.0,-2,22000,2,'mech'),('rds_kobra','Кобра ЭКП',1.0,-4,13000,1,'komendant'),
    ('scope_1p78','1П78 «Кашан» 2.8x',2.8,-8,45000,2,'komendant'),('scope_acog','ACOG 4x',4.0,-9,60000,3,'hacker'),
    ('scope_elcan','Elcan 1-4x',4.0,-10,80000,4,'hacker'),('scope_pso','ПСО-1 4x',4.0,-9,35000,2,'komendant'),
    ('scope_vudu','Vudu 1-6x',6.0,-11,95000,4,'hacker'),('scope_6x','Прицел 6x «Дедал»',6.0,-12,70000,3,'mech'),
    ('scope_8x','Прицел 8x «Пилад»',8.0,-14,90000,3,'mech'),('scope_10x','Прицел 10x Nightforce',10.0,-16,140000,4,'hacker'),
    ('scope_12x','Прицел 3-12x Vortex',12.0,-16,160000,4,'hacker'),('scope_3_9','Прицел 3-9x «Таёжник»',9.0,-13,48000,2,'babulya'),
    ('scope_25x','Прицел 5-25x Schmidt',25.0,-20,320000,5,'hacker'),('nvscope','Ночной прицел ПН-23',3.5,-18,260000,5,'komendant'),
]
for sid, nm, z, e, pr, lvl, tr in SIGHTS:
    mod(sid, nm, 'sight', dict(zoom=z, ergo=e, moa=-0.08 if z > 1 else -0.03, night=1 if sid=='nvscope' else 0), pr, lvl, tr, cls=['smg','ar','dmr','sniper','shotgun','lmg'], model='scope' if z>1 else 'reddot', w=2 if z>1 else 1, wt=0.5 if z>1 else 0.15)
mod('psight_rmr','Пистолетный коллиматор RMR','sight_p',dict(zoom=1.0, ergo=-1, moa=-0.03),28000,3,'hacker',cls=['pistol'],model='reddot',wt=0.05)
mod('psight_tritium','Тритиевый целик','sight_p',dict(moa=-0.02, night=0.3),9000,1,'mech',cls=['pistol'],model='',wt=0.02)
mod('psight_docter','Пистолетный коллиматор Docter','sight_p',dict(zoom=1.0, ergo=-1, moa=-0.03),22000,2,'mech',cls=['pistol'],model='reddot',wt=0.05)
# рукоятки
for gid, nm, st, pr, lvl, tr, cls in [
    ('grip_rk2','Рукоятка RK-2',dict(recoil=-0.05, ergo=5),12000,1,'mech',['smg','ar','dmr','lmg']),
    ('grip_angled','Угловая рукоятка AFG',dict(recoil=-0.03, ergo=8),14000,2,'mech',['smg','ar','dmr','lmg']),
    ('grip_rtm','Рукоятка RTM',dict(recoil=-0.07, ergo=3),18000,2,'komendant',['smg','ar','dmr','lmg']),
    ('grip_stubby','Короткая рукоятка Stubby',dict(recoil=-0.04, ergo=7),16000,2,'hacker',['smg','ar','dmr','lmg']),
    ('grip_vertical','Вертикальная рукоятка',dict(recoil=-0.06, ergo=2),8000,1,'babulya',['smg','ar','dmr','lmg']),
    ('grip_bipodgrip','Рукоятка-сошки',dict(recoil=-0.09, ergo=-4, weight=0.4),30000,3,'komendant',['ar','dmr','lmg']),
    ('grip_tango','Рукоятка Tango Down',dict(recoil=-0.06, ergo=6),24000,3,'hacker',['smg','ar','dmr','lmg']),
    ('grip_carbon','Карбоновая рукоятка',dict(recoil=-0.05, ergo=9, weight=-0.1),40000,4,'hacker',['smg','ar','dmr','lmg']),
]:
    mod(gid, nm, 'grip', st, pr, lvl, tr, cls=cls, model='grip')
# приклады
for sid, nm, st, pr, lvl, tr, fam in [
    ('stock_ak_wood','Приклад АК деревянный',dict(recoil=-0.04, ergo=0),5000,1,'babulya',['ak']),
    ('stock_ak_poly','Приклад АК полимерный',dict(recoil=-0.06, ergo=3, weight=-0.1),12000,1,'mech',['ak']),
    ('stock_ak_zenit','Приклад «Зенит» ПТ-1',dict(recoil=-0.10, ergo=4),28000,2,'mech',['ak']),
    ('stock_ak_magpul','Приклад Magpul Zhukov',dict(recoil=-0.09, ergo=6),32000,3,'hacker',['ak']),
    ('stock_ak_folding','Приклад складной «Сокол»',dict(recoil=-0.05, ergo=8),18000,2,'komendant',['ak']),
    ('stock_ar_ctr','Приклад Magpul CTR',dict(recoil=-0.07, ergo=5),20000,2,'mech',['ar15']),
    ('stock_ar_ubr','Приклад Magpul UBR',dict(recoil=-0.11, ergo=1),35000,3,'komendant',['ar15']),
    ('stock_ar_moe','Приклад Magpul MOE',dict(recoil=-0.06, ergo=6),16000,1,'mech',['ar15']),
    ('stock_ar_prs','Приклад PRS снайперский',dict(recoil=-0.13, ergo=-4),48000,4,'hacker',['ar15']),
    ('stock_hk_a3','Приклад HK A3 телескопический',dict(recoil=-0.07, ergo=4),22000,2,'mech',['hk']),
    ('stock_hk_fixed','Приклад HK фиксированный',dict(recoil=-0.10, ergo=0),18000,2,'komendant',['hk']),
    ('stock_scar_std','Приклад SCAR стандартный',dict(recoil=-0.08, ergo=3),25000,3,'mech',['scar']),
    ('stock_gen_pad','Затыльник амортизирующий',dict(recoil=-0.05, ergo=1),9000,1,'babulya',['generic','ak','ar15','hk','scar']),
    ('stock_gen_cheek','Щека на приклад',dict(recoil=-0.02, ergo=5),7000,1,'babulya',['generic','ak','ar15','hk','scar']),
    ('stock_gen_sniper','Приклад снайперский регулируемый',dict(recoil=-0.12, ergo=-2),60000,4,'komendant',['generic']),
]:
    mod(sid, nm, 'stock', st, pr, lvl, tr, fam=fam, model='stock', w=2, wt=0.4)
# цевья
for hid, nm, st, pr, lvl, tr, fam in [
    ('hg_ak_wood','Цевьё АК деревянное',dict(ergo=0),4000,1,'babulya',['ak']),
    ('hg_ak_b10','Цевьё «Зенит» Б-10М',dict(ergo=5, recoil=-0.03),26000,2,'mech',['ak']),
    ('hg_ak_magpul','Цевьё Magpul MOE AK',dict(ergo=7),22000,2,'hacker',['ak']),
    ('hg_ak_alfa','Цевьё «Альфа» с планками',dict(ergo=3, recoil=-0.05),35000,3,'komendant',['ak']),
    ('hg_ar_mlok','Цевьё M-LOK 13"',dict(ergo=8, recoil=-0.02),30000,2,'mech',['ar15']),
    ('hg_ar_ris','Цевьё RIS II',dict(ergo=2, recoil=-0.06),38000,3,'komendant',['ar15']),
    ('hg_ar_slim','Цевьё облегчённое карбон',dict(ergo=12, weight=-0.2),55000,4,'hacker',['ar15']),
    ('hg_hk_std','Цевьё HK с планками',dict(ergo=4, recoil=-0.03),20000,2,'mech',['hk']),
    ('hg_scar_rail','Цевьё SCAR удлинённое',dict(ergo=3, recoil=-0.05),32000,3,'mech',['scar']),
]:
    mod(hid, nm, 'handguard', st, pr, lvl, tr, fam=fam, model='handguard', w=2, wt=0.3)
# стволы
for bid, nm, st, pr, lvl, tr, fam in [
    ('barrel_ak_short','Ствол АК укороченный',dict(ergo=8, recoil=0.08, velocity=-0.08, moa=0.3),20000,2,'mech',['ak']),
    ('barrel_ak_long','Ствол АК удлинённый',dict(ergo=-6, recoil=-0.06, velocity=0.06, moa=-0.2),28000,3,'mech',['ak']),
    ('barrel_ar_105','Ствол 10.5"',dict(ergo=8, recoil=0.07, velocity=-0.1, moa=0.3),24000,2,'mech',['ar15']),
    ('barrel_ar_18','Ствол 18" тяжёлый',dict(ergo=-8, recoil=-0.08, velocity=0.08, moa=-0.35),45000,3,'hacker',['ar15']),
    ('barrel_hk_long','Ствол HK удлинённый',dict(ergo=-5, recoil=-0.05, velocity=0.05, moa=-0.2),30000,3,'mech',['hk']),
    ('barrel_gen_match','Матчевый ствол',dict(ergo=-4, recoil=-0.03, moa=-0.4),70000,4,'hacker',['generic','scar']),
]:
    mod(bid, nm, 'barrel', st, pr, lvl, tr, fam=fam, model='barrel', w=2, wt=0.6)
# фонари/ЛЦУ
for lid, nm, st, pr, lvl, tr in [
    ('light_x300','Фонарь X300',dict(light=1, ergo=-2),15000,1,'mech'),('light_ltr','Фонарь-ЛЦУ ЛТР-1',dict(light=1, laser=1, ergo=-3, moa=-0.02),28000,2,'komendant'),
    ('laser_red','ЛЦУ красный',dict(laser=1, ergo=-1, moa=-0.03),9000,1,'babulya'),('laser_green','ЛЦУ зелёный «Зенит»',dict(laser=1, ergo=-1, moa=-0.05),20000,2,'mech'),
    ('laser_peq','ЛЦУ PEQ-15 с ИК',dict(laser=1, light=1, ergo=-3, moa=-0.05, night=0.5),60000,4,'hacker'),
]:
    mod(lid, nm, 'light', st, pr, lvl, tr, cls=['pistol','smg','ar','dmr','shotgun','lmg'], model='laser', wt=0.1)
# сошки
mod('bipod_harris','Сошки Harris','bipod',dict(recoil=-0.15, ergo=-10, weight=0.4),30000,2,'mech',cls=['dmr','sniper','lmg'],model='bipod',w=2,wt=0.4)
mod('bipod_atlas','Сошки Atlas','bipod',dict(recoil=-0.20, ergo=-8, weight=0.3),65000,4,'hacker',cls=['dmr','sniper','lmg'],model='bipod',w=2,wt=0.3)
mod('bipod_light','Сошки лёгкие','bipod',dict(recoil=-0.10, ergo=-5, weight=0.2),14000,1,'babulya',cls=['dmr','sniper','lmg'],model='bipod',w=2,wt=0.2)

# ---------------------------------------------------------------- ПРЕДМЕТЫ
items = []
def item(id, name, type_, w, h, wt, price, trader=None, lvl=1, rarity=1, desc='', **props):
    items.append(dict(id=id, name=name, type=type_, w=w, h=h, weight=wt, price=price, trader=trader, lvl=lvl, rarity=rarity, desc=desc, props=props))
# медицина: heal — хп, time — секунды, uses — использований, stopBleed, fixFracture, pain — секунды обезболивания
item('bandage','Бинт','med',1,1,0.05,1500,'doc',1,1,'Останавливает лёгкое кровотечение.',uses=2,time=2,stopBleed=1)
item('bandage_army','Бинт армейский','med',1,1,0.08,3500,'doc',2,2,'Останавливает любое кровотечение.',uses=3,time=2,stopBleed=2)
item('tourniquet','Жгут','med',1,1,0.1,4500,'doc',2,2,'Останавливает тяжёлое кровотечение.',uses=1,time=3,stopBleed=2)
item('splint','Шина','med',1,1,0.15,2500,'doc',1,1,'Фиксирует перелом.',uses=1,time=4,fixFracture=1)
item('ai2','Аптечка АИ-2','med',1,1,0.2,6000,'doc',1,1,'Простая аптечка. Лечит понемногу.',heal=100,uses=4,time=3)
item('salewa','Аптечка Salewa','med',1,2,0.6,22000,'doc',2,2,'Хорошая аптечка. Останавливает кровь.',heal=400,uses=10,time=3,stopBleed=1)
item('ifak','IFAK','med',1,1,0.4,20000,'doc',2,3,'Компактная тактическая аптечка.',heal=300,uses=8,time=3,stopBleed=2)
item('grizzly','Аптечка Grizzly','med',2,2,1.2,45000,'doc',3,4,'Полная аптечка: лечит всё.',heal=1800,uses=20,time=5,stopBleed=2,fixFracture=1)
item('painkillers','Обезболивающее','med',1,1,0.05,3000,'doc',1,1,'Убирает эффекты боли на 4 минуты.',uses=4,time=1,pain=240)
item('morphine','Морфин','med',1,1,0.05,9000,'doc',2,3,'Сильное обезболивающее на 5 минут.',uses=1,time=1,pain=300)
item('adrenaline','Адреналин','med',1,1,0.05,12000,'doc',3,3,'Выносливость и боль на 3 минуты.',uses=1,time=1,pain=180,stamina=1)
item('surgkit','Хирургический набор','med',2,3,1.5,38000,'doc',3,4,'Восстанавливает уничтоженную часть тела.',uses=5,time=12,surgery=1)
item('cms','Набор CMS','med',2,2,1.0,25000,'doc',2,3,'Хирургия в поле, похуже.',uses=5,time=16,surgery=0.6)
# еда/вода: energy, hydration
for fid, nm, w_, h_, wt_, pr, tr, lvl, rar, en, hy in [
    ('water','Вода 0.5 л',1,1,0.5,1500,'babulya',1,1,0,60),('water_big','Вода 1.5 л',1,2,1.5,3500,'babulya',1,1,0,100),
    ('cola','Газировка',1,1,0.4,2200,'babulya',1,1,10,40),('juice','Сок',1,1,0.4,2500,'babulya',1,2,15,50),
    ('energy_drink','Энергетик',1,1,0.3,5000,'babulya',2,2,20,30),('kvass','Квас',1,2,1.2,3000,'babulya',1,1,10,80),
    ('bread','Хлеб',1,1,0.3,900,'babulya',1,1,30,0),('tushenka','Тушёнка',1,1,0.4,3500,'babulya',1,1,60,0),
    ('condensed','Сгущёнка',1,1,0.4,2800,'babulya',1,1,45,5),('crackers','Сухари',1,1,0.2,1200,'babulya',1,1,25,-5),
    ('mre','Сухпаёк ИРП',1,2,1.2,9000,'komendant',2,2,120,10),('chocolate','Шоколад',1,1,0.1,1800,'babulya',1,1,25,0),
    ('salami','Колбаса',1,1,0.4,3200,'babulya',1,1,50,0),('cheburek','Чебурек из столовой',1,1,0.2,800,'babulya',1,1,35,0),
]:
    item(fid, nm, 'food', w_, h_, wt_, pr, tr, lvl, rar, '', energy=en, hydration=hy, time=3)
# ценности
for vid, nm, w_, h_, wt_, pr, rar in [
    ('gold_chain','Золотая цепь',1,1,0.05,25000,3),('rolex','Часы Rolex',1,1,0.1,60000,4),('bitcoin','Биткоин (физ.)',1,1,0.01,180000,5),
    ('gpu','Видеокарта',2,1,1.0,120000,4),('cpu','Процессор',1,1,0.05,40000,3),('ram','Модуль памяти',1,1,0.02,12000,2),
    ('ssd','SSD-накопитель',1,1,0.05,18000,3),('phone','Смартфон',1,1,0.2,15000,2),('tablet','Планшет',2,1,0.5,30000,3),
    ('laptop','Ноутбук',2,2,2.0,55000,3),('cigs','Сигареты блок',1,2,0.3,9000,1),('vodka','Водка',1,2,0.9,7000,1),
    ('whiskey','Виски',1,2,1.0,14000,2),('lion','Статуэтка льва',2,3,3.0,95000,4),('skull','Череп хрустальный',1,2,1.0,45000,4),
    ('ring','Кольцо с бриллиантом',1,1,0.01,35000,4),('lighter','Зажигалка Zippo',1,1,0.05,6000,2),('wallet','Кошелёк',1,1,0.1,4000,1),
    ('cat_fig','Статуэтка кота',1,2,0.6,18000,2),('medal','Орден',1,1,0.05,22000,3),('icon','Икона старинная',2,2,0.8,70000,4),
    ('camera','Фотоаппарат',1,1,0.5,25000,3),('chess','Шахматы',2,2,1.0,8000,2),('book_rare','Редкая книга',1,2,0.5,12000,2),
    ('diploma','Диплом с печатью',1,1,0.05,9000,2),('zachetka','Зачётка с пятёрками',1,1,0.02,15000,3),
    ('amulet','Амулет шахтёра',1,1,0.05,30000,3),('record','Виниловая пластинка',2,2,0.3,7000,2),
]:
    item(vid, nm, 'valuable', w_, h_, wt_, pr, 'fence', 1, rar)
# бартер/крафт
for bid_, nm, w_, h_, wt_, pr, rar in [
    ('bolts','Болты',1,1,0.3,2500,1),('screws','Шурупы',1,1,0.2,2000,1),('nuts','Гайки',1,1,0.3,2200,1),('wires','Провода',1,1,0.2,3000,1),
    ('duct_tape','Изолента',1,1,0.1,1500,1),('electronics','Электронные компоненты',1,1,0.2,9000,2),('circuit','Печатная плата',1,1,0.1,14000,3),
    ('capacitors','Конденсаторы',1,1,0.1,6000,2),('wd40','WD-40',1,2,0.4,4000,1),('fuel','Канистра бензина',2,3,8.0,20000,2),
    ('propane','Баллон пропана',2,3,12.0,22000,2),('toolset','Набор инструментов',2,2,3.0,25000,3),('pliers','Плоскогубцы',1,1,0.3,2500,1),
    ('wrench','Разводной ключ',1,2,0.6,3500,1),('powerbank','Пауэрбанк',1,1,0.3,8000,2),('batteries','Батарейки АА',1,1,0.1,2000,1),
    ('car_battery','Автомобильный аккумулятор',3,2,15.0,18000,2),('lamp','Лампочки',1,1,0.1,1200,1),('cloth','Ткань',1,1,0.3,1500,1),
    ('hose','Шланг',2,1,0.8,3000,1),('pipe','Труба',1,3,2.0,2500,1),('nails','Гвозди',1,1,0.5,1500,1),('gunpowder','Порох',1,1,0.5,12000,3),
    ('cordura','Кордура',2,1,0.5,6000,2),('kevlar','Кевларовая пластина',2,2,2.0,30000,4),('matches','Спички',1,1,0.02,500,1),
    ('soap','Мыло',1,1,0.1,1200,1),('textbook','Учебник по матану',1,2,0.8,2000,1),('chalk','Мел',1,1,0.1,300,1),('helmet_miner','Каска шахтёра',2,2,0.8,5000,1),
]:
    item(bid_, nm, 'barter', w_, h_, wt_, pr, 'fence', 1, rar)
# ключи: map — карта, где нужен
for kid, nm, mp, pr, rar in [
    ('key_mirea_server','Ключ от серверной МИРЭА','mirea',60000,4),('key_mirea_dean','Ключ от деканата','mirea',35000,3),
    ('key_mirea_lab','Ключ от лаборатории','mirea',25000,2),('key_mirea_storage','Ключ от склада кафедры','mirea',15000,2),
    ('key_donetsk_bank','Ключ от хранилища банка','donetsk',120000,5),('key_donetsk_office','Ключ от офиса','donetsk',30000,3),
    ('key_donetsk_pharmacy','Ключ от аптеки','donetsk',20000,2),('key_donetsk_theatre','Ключ от гримёрки театра','donetsk',40000,3),
    ('key_factory_director','Ключ от кабинета директора','factory',50000,4),('key_factory_tools','Ключ от инструменталки','factory',18000,2),
    ('key_suburb_cottage','Ключ от коттеджа','suburb',35000,3),('key_suburb_garage','Ключ от гаража','suburb',15000,2),
    ('key_mall_jewelry','Ключ от ювелирного','mall',90000,5),('key_mall_security','Ключ от охраны ТЦ','mall',30000,3),
    ('key_metro_control','Ключ от диспетчерской','metro',45000,4),('key_metro_armory','Ключ от оружейной','metro',110000,5),
]:
    item(kid, nm, 'key', 1, 1, 0.01, pr, 'fence', 3, rar, 'Открывает запертую дверь.', map=mp, uses=10)
# броня: cls — класс, dur — прочность, zones
for aid, nm, cls_, dur, w_, h_, wt_, pr, tr, lvl, rar, mov in [
    ('armor_paca','Бронежилет PACA',2,50,3,3,3.5,25000,'komendant',1,1,-0.02),('armor_6b2','6Б2',2,60,3,3,4.0,28000,'komendant',1,1,-0.02),
    ('armor_kirasa','Кираса-Н',3,70,3,3,6.0,45000,'komendant',2,2,-0.04),('armor_module','Модуль-3М',3,60,3,3,5.0,40000,'babulya',1,2,-0.03),
    ('armor_6b23','6Б23-1',4,85,3,3,8.5,75000,'komendant',2,3,-0.06),('armor_korund','Корунд-ВМ',5,95,3,3,10.0,120000,'komendant',3,4,-0.08),
    ('armor_gzhel','Гжель-К',5,80,3,3,9.0,110000,'fence',3,4,-0.07),('armor_6b43','6Б43',6,110,3,4,14.0,210000,'komendant',4,5,-0.11),
    ('armor_slick','Slick Plate Carrier',6,80,3,3,6.0,260000,'hacker',4,5,-0.05),('armor_thor','THOR Integrated',6,120,3,4,13.0,320000,'hacker',4,5,-0.10),
]:
    item(aid, nm, 'armor', w_, h_, wt_, pr, tr, lvl, rar, f'Класс {cls_}.', armorClass=cls_, durability=dur, speed=mov, zones=['thorax','stomach'])
for hid_, nm, cls_, dur, wt_, pr, tr, lvl, rar in [
    ('helmet_cap','Кепка',0,0,0.1,1000,'babulya',1,1),('helmet_ssh68','СШ-68',2,30,1.5,12000,'komendant',1,1),('helmet_kolpak','Колпак-1С',3,40,2.0,25000,'komendant',2,2),
    ('helmet_kiver','Кивер-М',3,45,1.8,30000,'komendant',2,2),('helmet_6b47','6Б47 «Ратник»',3,50,1.2,45000,'komendant',2,3),('helmet_fast','FAST MT',4,55,1.5,90000,'hacker',3,4),
    ('helmet_altyn','Алтын',5,80,4.0,160000,'komendant',4,5),('helmet_maska','Маска-1Щ',5,90,5.0,180000,'fence',4,5),('helmet_vulkan','Вулкан-5',6,100,5.5,220000,'komendant',4,5),
]:
    item(hid_, nm, 'helmet', 2, 2, wt_, pr, tr, lvl, rar, f'Класс {cls_}.', armorClass=cls_, durability=dur, zones=['head'])
for bid_, nm, cw, ch, w_, h_, wt_, pr, tr, lvl, rar in [
    ('bag_school','Школьный рюкзак',4,4,3,3,0.5,5000,'babulya',1,1),('bag_tactical','Тактический рюкзак',5,5,3,4,1.0,15000,'babulya',1,2),
    ('bag_mbss','MBSS',5,6,3,4,1.2,25000,'komendant',2,2),('bag_pilgrim','Pilgrim',7,7,4,5,2.5,45000,'babulya',3,3),
    ('bag_tri_zip','Tri-Zip',6,7,4,4,1.8,38000,'hacker',2,3),('bag_attack','Attack 2',7,8,4,5,2.8,60000,'komendant',3,4),
    ('bag_expedition','Экспедиционный',8,9,5,6,3.5,85000,'fence',4,4),('bag_gym','Спортивная сумка',5,4,4,2,0.4,4000,'babulya',1,1),
]:
    item(bid_, nm, 'backpack', w_, h_, wt_, pr, tr, lvl, rar, f'{cw}x{ch} ячеек.', cellsW=cw, cellsH=ch)
for rid, nm, cw, ch, cls_, wt_, pr, tr, lvl, rar in [
    ('rig_belt','Разгрузочный пояс',4,2,0,0.5,6000,'babulya',1,1),('rig_scav','Разгрузка мародёра',5,2,0,0.8,9000,'babulya',1,1),
    ('rig_smersh','Разгрузка «Смерш»',6,3,0,1.2,22000,'komendant',2,2),('rig_ana','Ana Tactical M2',6,3,0,1.3,25000,'babulya',2,2),
    ('rig_6b5','6Б5-15 бронеразгрузка',5,3,3,7.0,60000,'komendant',3,3),('rig_tv110','TV-110 Plate Carrier',6,3,5,8.0,150000,'hacker',4,4),
    ('rig_avs','AVS Rig',7,3,5,9.0,190000,'komendant',4,5),('rig_blackrock','Blackrock',7,3,0,1.6,40000,'fence',3,3),
]:
    item(rid, nm, 'rig', 3, 3, wt_, pr, tr, lvl, rar, f'{cw}x{ch} ячеек.', cellsW=cw, cellsH=ch, armorClass=cls_, durability=60 if cls_ else 0, zones=['thorax','stomach'] if cls_ else [])
for tid, nm, dmg, radius, pr, tr, lvl, rar, kind in [
    ('gren_f1','Граната Ф-1',150,7,12000,'komendant',2,2,'frag'),('gren_rgd5','Граната РГД-5',120,5,9000,'komendant',1,1,'frag'),
    ('gren_m67','Граната M67',140,6,11000,'hacker',2,2,'frag'),('gren_flash','Светошумовая «Заря»',0,8,8000,'komendant',2,2,'flash'),
    ('gren_smoke','Дымовая РДГ-2',0,6,5000,'komendant',1,1,'smoke'),('gren_vog','ВОГ-17 (ручной)',90,4,15000,'fence',3,3,'frag'),
]:
    item(tid, nm, 'throwable', 1, 1, 0.5, pr, tr, lvl, rar, '', dmg=dmg, radius=radius, kind=kind, fuse=3.5)
item('repair_kit','Ремкомплект оружейный','tool',2,2,1.0,35000,'mech',2,3,'Восстанавливает прочность оружия.',repair=60)
item('armor_repair','Ремкомплект брони','tool',2,2,1.5,40000,'komendant',2,3,'Восстанавливает прочность брони.',repair=50)
item('flashlight','Фонарик','tool',1,1,0.2,3000,'babulya',1,1,'Ручной фонарь.')
item('nvg','ПНВ ПНВ-10Т','tool',2,2,0.8,180000,'hacker',4,5,'Прибор ночного видения (надевается на шлем).',night=1)
item('compass','Компас','tool',1,1,0.1,2000,'babulya',1,1,'Показывает направление.')
item('intel','Разведданные','intel',1,1,0.1,25000,'hacker',2,3,'Папка с документами.')
item('flash_drive','Флешка с данными','intel',1,1,0.02,30000,'hacker',2,3,'Нужна хакеру.')
item('secure_case','Защищённый контейнер','container',2,2,0.5,150000,'fence',3,4,'Сохраняет содержимое при смерти.',cellsW=2,cellsH=2,secure=1)
item('secure_case_big','Защищённый контейнер «Гамма»','container',3,3,0.8,500000,'fence',5,5,'Сохраняет содержимое при смерти.',cellsW=3,cellsH=3,secure=1)
item('ammo_box','Ящик для патронов','container',2,2,0.8,20000,'mech',2,2,'Только патроны.',cellsW=3,cellsH=3,only='ammo')
item('med_case','Медицинский кейс','container',2,2,0.8,25000,'doc',2,2,'Только медицина.',cellsW=3,cellsH=3,only='med')

# ---------------------------------------------------------------- НАВЫКИ (качаются использованием)
skills = []
def skill(id, name, group, desc, perLevel, milestones):
    skills.append(dict(id=id, name=name, group=group, desc=desc, perLevel=perLevel, milestones=milestones, maxLevel=51))
skill('endurance','Выносливость','Физика','Качается бегом. Больше запас сил, медленнее устаёшь.',dict(maxStamina=0.02, staminaDrain=-0.006),{10:'Дыхание не сбивается при прицеливании после бега',20:'+10% к скорости восстановления сил',30:'Прыжки не тратят силы',40:'Иммунитет к одышке',50:'Бесконечный бег с лёгким грузом'})
skill('strength','Сила','Физика','Качается переноской тяжестей и ближним боем. Больше грузоподъёмность и урон в ближнем бою.',dict(carryWeight=0.015, meleeDamage=0.01, throwDistance=0.01),{10:'+5 кг к переносимому весу',20:'Скорость бега с перегрузом выше',30:'Удар кулаком/ножом сбивает с ног',40:'+20 кг к переносимому весу',50:'Перегруз не замедляет'})
skill('vitality','Живучесть','Физика','Качается получением урона. Ниже шанс кровотечения, выше здоровье.',dict(bleedChance=-0.012, maxHealth=0.003),{10:'Кровотечения останавливаются сами через 60 с',20:'Шанс перелома −30%',30:'+5% к общему здоровью',40:'Лёгкие кровотечения останавливаются сами',50:'Урон от падения −50%'})
skill('health','Здоровье','Физика','Качается лечением. Быстрее регенерация и лечение.',dict(healSpeed=0.015, regen=0.01),{10:'Пассивная регенерация 0.5 хп/с',20:'Еда даёт на 20% больше энергии',30:'Переломы не замедляют',40:'Регенерация 1 хп/с',50:'Лечение не прерывается уроном'})
skill('metabolism','Метаболизм','Физика','Качается едой. Медленнее голод и жажда.',dict(hungerRate=-0.01, hydrationRate=-0.01),{10:'Еда действует быстрее',20:'Еда не портится (−вес)',30:'Жажда на 30% медленнее',40:'Плохие эффекты от еды отсутствуют',50:'Голод и жажда замедлены вдвое'})
skill('stress','Стрессоустойчивость','Ментальное','Качается получением боли и близкими попаданиями. Меньше тряска и шок.',dict(painTolerance=0.015, swayHurt=-0.012),{10:'Боль не сбивает прицел',20:'Тремор при низком здоровье снижен',30:'Иммунитет к шоку от взрыва',40:'Светошумовые действуют вдвое меньше',50:'Нет штрафа к точности от боли'})
skill('perception','Восприятие','Ментальное','Качается осмотром контейнеров и дальними попаданиями. Дальше слышно и видно лут.',dict(hearRange=0.01, lootHighlight=0.02),{10:'Лут подсвечивается с 10 м',20:'Шаги врагов слышно дальше',30:'Компас показывает выходы',40:'Лут подсвечивается с 20 м',50:'Видно тип лута сквозь стены (5 м)'})
skill('attention','Внимательность','Ментальное','Качается поиском. Быстрее осмотр контейнеров, лучше находки.',dict(searchSpeed=0.015, lootQuality=0.005),{10:'Осмотр −10% времени',20:'Шанс редкого предмета +5%',30:'Видно содержимое ящика без осмотра',40:'Осмотр −30%',50:'Редкий лут +15%'})
skill('search','Мародёрство','Ментальное','Качается сбором предметов. Быстрее подбор и больше находок.',dict(pickupSpeed=0.02, lootCount=0.005),{10:'Подбор на бегу',20:'Карманы трупов открываются мгновенно',30:'+1 предмет в ящиках (шанс 10%)',40:'Подбор мгновенный',50:'+1 предмет в ящиках (шанс 30%)'})
skill('charisma','Харизма','Ментальное','Качается торговлей и квестами. Лучшие цены и репутация.',dict(tradePrice=-0.006, sellPrice=0.006, repGain=0.01),{10:'Цены −5%',20:'Торговцы дают квесты раньше',30:'Скупка +10%',40:'Цены −15%',50:'Доступ к секретному ассортименту'})
skill('intellect','Интеллект','Ментальное','Качается крафтом и осмотром предметов. Быстрее крафт и осмотр.',dict(craftSpeed=0.02, examineSpeed=0.02, hideoutSpeed=0.01),{10:'Крафт −10% времени',20:'Все предметы известны',30:'Улучшение убежища −20% ресурсов',40:'Крафт −30%',50:'Крафт выдаёт +1 (шанс 25%)'})
skill('memory','Память','Ментальное','Качается игрой. Навыки медленнее забываются при длительном простое.',dict(skillDecay=-0.02),{10:'Ваши навыки не падают 3 дня',20:'Карты запоминают найденные ключевые точки',30:'Квестовые точки отмечаются на компасе',40:'Навыки не падают 7 дней',50:'Навыки никогда не падают'})
skill('crafting','Ремесло','Практика','Качается крафтом в убежище. Дешевле и быстрее рецепты.',dict(craftCost=-0.01, craftSpeed=0.01),{10:'Открыты рецепты 2 уровня',20:'Разборка предметов возвращает больше',30:'Открыты рецепты 3 уровня',40:'Двойной крафт патронов',50:'Легендарные рецепты'})
skill('surgery','Хирургия','Практика','Качается хирургией. Быстрее и лучше восстановление частей тела.',dict(surgerySpeed=0.02, surgeryMaxHp=0.01),{10:'Хирургия −10% времени',20:'Часть тела после операции 60% хп',30:'Хирургия не прерывается',40:'Хирургия −30%',50:'Часть тела после операции 100% хп'})
skill('covert','Скрытность','Практика','Качается тихим передвижением. Тише шаги, меньше заметность.',dict(noise=-0.012, aiDetect=-0.01),{10:'Шаги в присяде беззвучны',20:'Боты замечают на 20% позже',30:'Открытие дверей бесшумно',40:'Шаги на ходу вдвое тише',50:'Невидимость для ботов при стоянии в тени'})
skill('immunity','Иммунитет','Практика','Качается отравлениями и стимуляторами. Меньше негативных эффектов.',dict(poisonResist=0.02, debuffDuration=-0.01),{10:'Еда не вызывает отравления',20:'Стимуляторы −негатив 50%',30:'Радиация −30%',40:'Иммунитет к тошноте',50:'Иммунитет к отравлениям'})
skill('recoil','Контроль отдачи','Оружие','Качается стрельбой. Меньше отдача всего оружия.',dict(recoil=-0.004),{10:'Горизонтальная отдача −10%',20:'Быстрее возврат ствола',30:'Отдача первых 3 выстрелов −20%',40:'Вертикальная отдача −15%',50:'Отдача −30%'})
skill('maintenance','Уход за оружием','Оружие','Качается ремонтом и стрельбой. Оружие изнашивается медленнее.',dict(durabilityLoss=-0.015, jamChance=-0.01),{10:'Ремонт в убежище дешевле',20:'Осечек меньше на 30%',30:'Ремкомплект чинит до 100%',40:'Износ −50%',50:'Оружие не клинит'})
for sid, nm, cls_ in [('pistols','Пистолеты','pistol'),('smg','Пистолеты-пулемёты','smg'),('ar','Штурмовые винтовки','ar'),('shotguns','Дробовики','shotgun'),('dmr','Марксманские винтовки','dmr'),('sniper','Снайперские винтовки','sniper'),('lmg','Пулемёты','lmg'),('melee','Ближний бой','melee')]:
    skill(sid, nm, 'Оружие', f'Качается стрельбой из класса «{nm}». Эргономика, отдача и перезарядка.', {f'ergo_{cls_}':0.4, f'recoil_{cls_}':-0.004, f'reload_{cls_}':-0.004, f'ads_{cls_}':-0.004}, {10:'Смена магазина быстрее',20:'Прицеливание быстрее',30:'Разброс при стрельбе от бедра −20%',40:'Отдача −15%',50:'Мастерство: особая анимация и −30% отдача'})
skill('throwables','Метание','Оружие','Качается бросками. Дальше и точнее броски гранат.',dict(throwDistance=0.015, throwAccuracy=0.01),{10:'Траектория броска видна',20:'Бросок на 15% дальше',30:'Гранаты не выпадают при ранении',40:'Бросок на 30% дальше',50:'Задержка взрывателя регулируется'})

# ---------------------------------------------------------------- ПЕРКИ (очки за уровень персонажа)
perks = []
def perk(id, branch, tier, name, desc, effects, requires=None, cost=1):
    perks.append(dict(id=id, branch=branch, tier=tier, name=name, desc=desc, effects=effects, requires=requires or [], cost=cost))
B = {'assault':'Штурмовик','scout':'Разведчик','medic':'Медик','engineer':'Инженер','trader':'Торговец','survivor':'Выживальщик'}
PERKS = {
 'assault': [
  (1,'steady_hands','Твёрдые руки','Отдача −5%',dict(recoil=-0.05)),(1,'quick_draw','Быстрая смена','Смена оружия +25%',dict(swapSpeed=0.25)),(1,'ammo_pouch','Подсумок','+30% патронов в стаке',dict(ammoStack=0.3)),
  (2,'suppressive','Подавление','Урон по врагам в укрытии +10%',dict(damage=0.05)),(2,'fast_mags','Быстрые магазины','Перезарядка −15%',dict(reload=-0.15)),(2,'hip_master','Стрельба от бедра','Разброс от бедра −25%',dict(hipSpread=-0.25)),
  (3,'juggernaut','Джаггернаут','Броня изнашивается на 25% медленнее',dict(armorWear=-0.25)),(3,'double_tap','Двойной выстрел','Первые два выстрела в очереди без отдачи',dict(firstShotRecoil=-0.5)),(3,'heavy_gunner','Тяжёлый стрелок','Пулемёты и .338: эргономика +15',dict(ergo_lmg=15, ergo_sniper=10)),
  (4,'berserk','Берсерк','При хп < 30%: урон +20%, отдача −20%',dict(lowHpDamage=0.2, lowHpRecoil=-0.2)),(4,'commander','Командир','Сопартийцы в радиусе 20 м получают отдачу −10%',dict(squadRecoil=-0.1)),(4,'last_stand','Последний рубеж','Смертельное ранение один раз за рейд оставляет 1 хп',dict(lastStand=1)),
 ],
 'scout': [
  (1,'light_step','Лёгкий шаг','Шум шагов −20%',dict(noise=-0.2)),(1,'marathon','Марафонец','Выносливость +15%',dict(maxStamina=0.15)),(1,'eagle_eye','Орлиный глаз','Зум прицелов +10%, лут видно дальше',dict(zoom=0.1, lootHighlight=0.3)),
  (2,'sprinter','Спринтер','Скорость бега +8%',dict(sprintSpeed=0.08)),(2,'climber','Альпинист','Высота прыжка +20%, урон от падения −30%',dict(jump=0.2, fallDamage=-0.3)),(2,'tracker','Следопыт','Враги, попавшие в вас, подсвечиваются 3 с',dict(trackHit=1)),
  (3,'ghost','Призрак','Боты замечают на 30% позже',dict(aiDetect=-0.3)),(3,'sixth_sense','Шестое чувство','Предупреждение, когда вас видит бот',dict(sixthSense=1)),(3,'exfil_expert','Мастер выхода','Время экстракции −50%',dict(extractTime=-0.5)),
  (4,'shadow','Тень','Прицел не светится, ЛЦУ не видно врагам, бесшумный присед',dict(noise=-0.4, aiDetect=-0.2)),(4,'cartographer','Картограф','Все выходы и точки квестов видны на компасе',dict(showExtracts=1)),(4,'survival_instinct','Инстинкт','Выносливость восстанавливается при хп < 40% вдвое быстрее',dict(lowHpStamina=1)),
 ],
 'medic': [
  (1,'first_aid','Первая помощь','Лечение на 15% быстрее',dict(healSpeed=0.15)),(1,'thick_skin','Толстая кожа','Шанс кровотечения −15%',dict(bleedChance=-0.15)),(1,'field_medic','Полевой медик','Аптечки +20% запаса',dict(medUses=0.2)),
  (2,'combat_medic','Боевой медик','Лечение сопартийцев в 2 раза быстрее',dict(allyHeal=1)),(2,'painless','Без боли','Обезболивающие действуют дольше на 50%',dict(painDuration=0.5)),(2,'regen','Регенерация','0.3 хп/с пассивно',dict(regen=0.3)),
  (3,'surgeon','Хирург','Хирургия на 40% быстрее',dict(surgerySpeed=0.4)),(3,'adrenaline_rush','Адреналин','При ранении на 5 с скорость +20%',dict(hitSpeed=0.2)),(3,'iron_lungs','Железные лёгкие','Задержка дыхания при прицеливании +100%',dict(holdBreath=1.0)),
  (4,'second_wind','Второе дыхание','Раз в рейд при смерти восстанавливается 50% хп',dict(secondWind=1)),(4,'guardian_angel','Ангел-хранитель','Сопартийцы получают +10% хп',dict(squadHp=0.1)),(4,'immortal','Бессмертный','Макс. здоровье +15%',dict(maxHealth=0.15)),
 ],
 'engineer': [
  (1,'gunsmith','Оружейник','Износ оружия −20%',dict(durabilityLoss=-0.2)),(1,'tinker','Самоделкин','Крафт на 20% быстрее',dict(craftSpeed=0.2)),(1,'scrapper','Разборщик','Разборка возвращает +30% ресурсов',dict(scrap=0.3)),
  (2,'mod_expert','Эксперт по модулям','Эргономика модулей +15%',dict(modErgo=0.15)),(2,'lockpick','Отмычка','Можно вскрывать двери без ключа (30 с)',dict(lockpick=1)),(2,'demolition','Подрывник','Урон гранат +25%',dict(grenadeDamage=0.25)),
  (3,'master_gunsmith','Мастер-оружейник','Модификации дешевле на 20%',dict(modPrice=-0.2)),(3,'builder','Строитель','Улучшения убежища −25% ресурсов',dict(hideoutCost=-0.25)),(3,'hacker_perk','Взломщик','Электронные замки открываются за 5 с',dict(hackSpeed=1)),
  (4,'legendary_craft','Легендарное ремесло','Доступны легендарные рецепты',dict(legendaryCraft=1)),(4,'overclock','Разгон','Оружие не клинит, скорострельность +5%',dict(jamChance=-1, rpm=0.05)),(4,'quartermaster','Завхоз','Склад +50 ячеек',dict(stashBonus=50)),
 ],
 'trader': [
  (1,'bargain','Торг','Цены у торговцев −5%',dict(tradePrice=-0.05)),(1,'appraiser','Оценщик','Скупка +10%',dict(sellPrice=0.1)),(1,'connections','Связи','Репутация растёт на 20% быстрее',dict(repGain=0.2)),
  (2,'fence_friend','Друг барыги','Ассортимент Гоши обновляется вдвое чаще',dict(fenceRefresh=1)),(2,'insurance','Страховка','Застрахованное оружие возвращается в 80% случаев',dict(insurance=0.8)),(2,'collector','Коллекционер','Ценности занимают меньше места (вес −30%)',dict(valuableWeight=-0.3)),
  (3,'wholesale','Опт','Патроны и медицина −15%',dict(ammoPrice=-0.15, medPrice=-0.15)),(3,'black_market','Чёрный рынок','Секретный ассортимент у всех торговцев',dict(blackMarket=1)),(3,'xp_dealer','Делец','Опыт за квесты +20%',dict(questXp=0.2)),
  (4,'tycoon','Магнат','Цены −15%, скупка +20%',dict(tradePrice=-0.15, sellPrice=0.2)),(4,'smuggler','Контрабандист','Защищённый контейнер +1 ряд',dict(secureBonus=1)),(4,'reputation','Репутация','Все торговцы +1 уровень лояльности',dict(traderLevelBonus=1)),
 ],
 'survivor': [
  (1,'camel','Верблюд','Жажда −25%',dict(hydrationRate=-0.25)),(1,'strong_back','Крепкая спина','+10 кг к переносимому весу',dict(carryWeightFlat=10)),(1,'night_owl','Сова','Ночью видно лучше',dict(night=0.3)),
  (2,'scavenger','Падальщик','+1 предмет в ящиках с шансом 15%',dict(lootCount=0.15)),(2,'cook','Повар','Еда даёт +30%',dict(foodBonus=0.3)),(2,'weatherproof','Непромокаемый','Дождь и холод не влияют',dict(weather=1)),
  (3,'lucky','Везунчик','Редкий лут +10%',dict(lootQuality=0.1)),(3,'hardened','Закалённый','Переломы −50%, кровотечения останавливаются сами',dict(fractureChance=-0.5, autoStopBleed=1)),(3,'packrat','Хомяк','Рюкзаки +2 ячейки',dict(bagBonus=2)),
  (4,'nine_lives','Девять жизней','После смерти сохраняется разгрузка',dict(keepRig=1)),(4,'wasteland_king','Король пустоши','Боссы роняют гарантированный редкий предмет',dict(bossLoot=1)),(4,'iron_will','Железная воля','Все навыки качаются на 25% быстрее',dict(skillXp=0.25)),
 ],
}
for br, lst in PERKS.items():
    prev_tier_ids = []
    cur = []
    last_tier = 0
    for tier, pid, nm, desc, eff in lst:
        if tier != last_tier:
            prev_tier_ids = cur; cur = []; last_tier = tier
        perk(f'{br}_{pid}', br, tier, nm, desc, eff, requires=prev_tier_ids[:], cost=tier)
        cur.append(f'{br}_{pid}')
branches = [dict(id=k, name=v) for k, v in B.items()]

# ---------------------------------------------------------------- УБЕЖИЩЕ
hideout = []
def module(id, name, desc, levels):
    hideout.append(dict(id=id, name=name, desc=desc, levels=levels))
def lvl(cost, items_, bonus, recipes=None, desc=''):
    return dict(money=cost, items=items_, bonus=bonus, recipes=recipes or [], desc=desc)
module('bedroom','Спальня','Отдых между рейдами.',[lvl(20000,{'cloth':3,'nails':2},dict(regen=0.1),desc='Регенерация в убежище'),lvl(80000,{'cloth':6,'lamp':2,'screws':4},dict(regen=0.2, maxStamina=0.05)),lvl(250000,{'cordura':4,'electronics':3},dict(regen=0.3, maxStamina=0.1, xpGain=0.05))])
module('workbench','Верстак','Ремонт и модификация оружия.',[lvl(30000,{'toolset':1,'bolts':5},dict(repairCost=-0.1),['craft_ammo_9x19','craft_ammo_545']),lvl(120000,{'toolset':2,'wd40':3,'electronics':2},dict(repairCost=-0.25, modPrice=-0.05),['craft_ammo_556','craft_ammo_762','craft_supp']),lvl(400000,{'toolset':3,'kevlar':2,'circuit':2},dict(repairCost=-0.5, modPrice=-0.1),['craft_ammo_ap','craft_gun_rare'])])
module('medstation','Медпункт','Лечение после рейда и крафт медицины.',[lvl(25000,{'bandage':5,'soap':2},dict(healAfterRaid=1),['craft_bandage']),lvl(100000,{'salewa':2,'electronics':2},dict(healAfterRaid=1, medPrice=-0.1),['craft_ifak','craft_splint']),lvl(350000,{'surgkit':1,'circuit':2,'cloth':5},dict(healAfterRaid=1, medPrice=-0.2, healSpeed=0.1),['craft_grizzly','craft_morphine'])])
module('generator','Генератор','Питает остальные модули.',[lvl(40000,{'fuel':2,'wires':3,'car_battery':1},dict(power=1)),lvl(150000,{'fuel':4,'electronics':4,'pipe':2},dict(power=2, craftSpeed=0.1)),lvl(450000,{'propane':2,'circuit':3,'capacitors':4},dict(power=3, craftSpeed=0.25))])
module('water','Сборщик воды','Крафт воды и снижение жажды.',[lvl(20000,{'hose':2,'pipe':1},dict(),['craft_water']),lvl(90000,{'hose':4,'pliers':1,'capacitors':2},dict(hydrationRate=-0.1),['craft_water_big']),lvl(200000,{'electronics':2,'pipe':4},dict(hydrationRate=-0.2),['craft_energy_drink'])])
module('kitchen','Кухня','Крафт еды.',[lvl(15000,{'propane':1,'matches':2},dict(),['craft_bread']),lvl(60000,{'lamp':2,'wires':2},dict(hungerRate=-0.1),['craft_tushenka']),lvl(180000,{'electronics':2,'cloth':2},dict(hungerRate=-0.2, foodBonus=0.2),['craft_mre'])])
module('range','Тир','Тренировка стрелковых навыков.',[lvl(50000,{'nails':5,'pipe':3},dict(weaponSkillXp=0.1)),lvl(180000,{'kevlar':1,'electronics':3},dict(weaponSkillXp=0.25)),lvl(500000,{'circuit':3,'capacitors':3,'kevlar':2},dict(weaponSkillXp=0.5, recoil=-0.03))])
module('gym','Спортзал','Тренировка физических навыков.',[lvl(40000,{'pipe':4,'nuts':4},dict(physSkillXp=0.1)),lvl(150000,{'car_battery':1,'wires':3},dict(physSkillXp=0.25, maxStamina=0.05)),lvl(400000,{'electronics':3,'kevlar':1},dict(physSkillXp=0.5, carryWeightFlat=5))])
module('intel','Разведцентр','Информация о картах, расписание боссов, квесты хакера.',[lvl(80000,{'laptop':1,'intel':2},dict(showExtracts=1, questXp=0.05)),lvl(250000,{'laptop':2,'circuit':2,'flash_drive':1},dict(showExtracts=1, questXp=0.15, bossInfo=1)),lvl(600000,{'gpu':1,'circuit':4,'intel':5},dict(showExtracts=1, questXp=0.3, bossInfo=1, lootHighlight=0.5))])
module('security','Охрана','Склад не теряет предметы, страховка дешевле.',[lvl(30000,{'nails':5,'lamp':2},dict(insurancePrice=-0.1)),lvl(120000,{'camera':1,'wires':4},dict(insurancePrice=-0.25)),lvl(300000,{'electronics':3,'kevlar':2},dict(insurancePrice=-0.5, stashBonus=20))])
module('library','Библиотека','Опыт и интеллект.',[lvl(30000,{'textbook':3,'book_rare':1},dict(xpGain=0.03, skillXp=0.05)),lvl(100000,{'textbook':6,'book_rare':3},dict(xpGain=0.06, skillXp=0.1)),lvl(250000,{'book_rare':6,'diploma':1},dict(xpGain=0.1, skillXp=0.2))])
module('bitcoin','Ферма','Пассивный доход.',[lvl(200000,{'gpu':2,'circuit':2,'wires':5},dict(income=1500)),lvl(600000,{'gpu':6,'circuit':4,'capacitors':4},dict(income=5000)),lvl(1500000,{'gpu':15,'circuit':8,'cpu':4},dict(income=14000))])
recipes = [
    dict(id='craft_ammo_9x19', name='Патроны 9x19 x60', time=600, inputs={'gunpowder':1,'bolts':1}, output={'ammo_9x19_std':60}),
    dict(id='craft_ammo_545', name='Патроны 5.45 x60', time=900, inputs={'gunpowder':1,'nuts':1}, output={'ammo_5.45x39_std':60}),
    dict(id='craft_ammo_556', name='Патроны 5.56 x60', time=900, inputs={'gunpowder':1,'nuts':1}, output={'ammo_5.56x45_std':60}),
    dict(id='craft_ammo_762', name='Патроны 7.62x39 x60', time=900, inputs={'gunpowder':1,'bolts':1}, output={'ammo_7.62x39_std':60}),
    dict(id='craft_ammo_ap', name='Бронебойные 5.45 x30', time=1800, inputs={'gunpowder':2,'kevlar':1}, output={'ammo_5.45x39_ap':30}),
    dict(id='craft_supp', name='Самодельный глушитель 9 мм', time=1200, inputs={'pipe':1,'cloth':2,'duct_tape':1}, output={'supp_pist9_1':1}),
    dict(id='craft_gun_rare', name='Сборка АК-12', time=3600, inputs={'ak74m':1,'toolset':1,'kevlar':1,'electronics':2}, output={'ak12':1}),
    dict(id='craft_bandage', name='Бинты x3', time=300, inputs={'cloth':1}, output={'bandage':3}),
    dict(id='craft_ifak', name='IFAK', time=900, inputs={'bandage_army':2,'duct_tape':1,'soap':1}, output={'ifak':1}),
    dict(id='craft_splint', name='Шина', time=200, inputs={'pipe':1,'cloth':1}, output={'splint':2}),
    dict(id='craft_grizzly', name='Grizzly', time=2400, inputs={'salewa':2,'ifak':1,'cloth':3}, output={'grizzly':1}),
    dict(id='craft_morphine', name='Морфин', time=1800, inputs={'painkillers':3,'chalk':1}, output={'morphine':2}),
    dict(id='craft_water', name='Вода 0.5 л x2', time=600, inputs={}, output={'water':2}),
    dict(id='craft_water_big', name='Вода 1.5 л', time=900, inputs={}, output={'water_big':1}),
    dict(id='craft_energy_drink', name='Энергетик x2', time=1200, inputs={'juice':1,'batteries':1}, output={'energy_drink':2}),
    dict(id='craft_bread', name='Хлеб x2', time=600, inputs={'water':1}, output={'bread':2}),
    dict(id='craft_tushenka', name='Тушёнка x3', time=1200, inputs={'salami':1,'water':1}, output={'tushenka':3}),
    dict(id='craft_mre', name='ИРП', time=1800, inputs={'tushenka':1,'crackers':1,'chocolate':1}, output={'mre':1}),
]

# ---------------------------------------------------------------- ВРАГИ
enemies = [
    dict(id='scav', name='Мародёр', hp=90, armorClass=0, accuracy=0.35, reaction=0.9, speed=3.2, aggression=0.6, weapons=['pm','tt33','toz106','kedr','aks74u','mp133','vpo136','sks','mosin','akms','fort12','uzi'], loot=['food','barter','valuable','med','ammo'], lootCount=[1,3], xp=60, boss=False),
    dict(id='scav_sniper', name='Мародёр-снайпер', hp=90, armorClass=0, accuracy=0.6, reaction=1.2, speed=2.8, aggression=0.2, weapons=['mosin','sks','vpo215','svd'], loot=['ammo','food','barter'], lootCount=[1,2], xp=110, boss=False, sniper=True),
    dict(id='raider', name='Рейдер', hp=140, armorClass=4, accuracy=0.55, reaction=0.6, speed=3.6, aggression=0.9, weapons=['ak74m','ak105','m4a1','mp5','saiga12','vityaz','ak103','scarl','g36','aug'], loot=['ammo','med','mod','valuable','key'], lootCount=[2,4], xp=180, boss=False),
    dict(id='cultist', name='Ночной', hp=120, armorClass=3, accuracy=0.5, reaction=0.4, speed=4.4, aggression=1.0, weapons=['asval','vss','sr3m','knife','machete'], loot=['med','intel','valuable','mod'], lootCount=[2,3], xp=220, boss=False, nightOnly=True),
    dict(id='boss_zavhoz', name='Завхоз Палыч', hp=600, armorClass=5, accuracy=0.65, reaction=0.4, speed=3.8, aggression=1.0, weapons=['saiga12','ak12'], loot=['key','valuable','mod','weapon'], lootCount=[4,6], xp=1500, boss=True, map='mirea', guards=3, guardType='raider'),
    dict(id='boss_shakhtyor', name='Шахтёр', hp=700, armorClass=6, accuracy=0.6, reaction=0.5, speed=3.4, aggression=1.0, weapons=['pkm','akm'], loot=['key','valuable','weapon','armor'], lootCount=[4,6], xp=1800, boss=True, map='donetsk', guards=4, guardType='raider'),
    dict(id='boss_brigadir', name='Бригадир', hp=550, armorClass=4, accuracy=0.7, reaction=0.35, speed=4.0, aggression=1.0, weapons=['rpk16','vector45'], loot=['mod','weapon','key','barter'], lootCount=[4,6], xp=1500, boss=True, map='factory', guards=3, guardType='raider'),
    dict(id='boss_lesnik', name='Лесник', hp=450, armorClass=3, accuracy=0.9, reaction=0.3, speed=3.6, aggression=0.5, weapons=['sv98','ks23'], loot=['weapon','key','valuable','ammo'], lootCount=[3,5], xp=1400, boss=True, map='suburb', guards=2, guardType='scav_sniper', sniper=True),
    dict(id='boss_stepanych', name='Охранник Степаныч', hp=650, armorClass=5, accuracy=0.6, reaction=0.45, speed=3.6, aggression=1.0, weapons=['m4super','hk416'], loot=['key','valuable','armor','mod'], lootCount=[4,6], xp=1600, boss=True, map='mall', guards=4, guardType='raider'),
    dict(id='boss_mashinist', name='Машинист', hp=800, armorClass=6, accuracy=0.65, reaction=0.4, speed=3.2, aggression=1.0, weapons=['pkp','asval'], loot=['key','weapon','armor','valuable'], lootCount=[5,7], xp=2200, boss=True, map='metro', guards=5, guardType='cultist'),
]

# ---------------------------------------------------------------- КАРТЫ
maps = [
    dict(id='mirea', name='РТУ МИРЭА', kind='osm', osm='mirea', desc='Кампус на проспекте Вернадского. Учебные корпуса, лаборатории, столовая, общежитие. Серверная заперта.',
         raidMinutes=35, enemies=[10,16], lootDensity=1.0, bossChance=0.35, size=700,
         extracts=[dict(name='Метро «Юго-Западная»', pos=[-300,-280], cond=''),dict(name='Парковка у главного входа', pos=[280,-250], cond=''),dict(name='Задний двор (нужен ключ от склада)', pos=[-250,260], cond='key_mirea_storage'),dict(name='Крыша корпуса А (только вдвоём)', pos=[0,0], cond='coop'),dict(name='Автобус (платный, 5000)', pos=[300,250], cond='money:5000')],
         pois=[dict(name='Главный корпус',pos=[0,40]),dict(name='Столовая',pos=[120,-60]),dict(name='Общежитие',pos=[-200,120]),dict(name='Спорткомплекс',pos=[200,150]),dict(name='Библиотека',pos=[-80,-120]),dict(name='Серверная',pos=[30,60])]),
    dict(id='donetsk', name='Донецк. Центр', kind='osm', osm='donetsk', desc='Центр города: площадь, проспект, театр, гостиницы, банк. Много этажей, много окон, много крыш.',
         raidMinutes=45, enemies=[14,22], lootDensity=1.1, bossChance=0.3, size=1000,
         extracts=[dict(name='Вокзал (автобус)', pos=[-450,-420], cond=''),dict(name='Мост через Кальмиус', pos=[460,300], cond=''),dict(name='Подвал театра (ключ)', pos=[60,120], cond='key_donetsk_theatre'),dict(name='Вертолётная площадка (только вдвоём)', pos=[-100,400], cond='coop'),dict(name='Такси (платный, 8000)', pos=[420,-380], cond='money:8000')],
         pois=[dict(name='Площадь Ленина',pos=[0,0]),dict(name='Театр',pos=[60,120]),dict(name='Банк',pos=[-150,-60]),dict(name='Гостиница',pos=[150,-100]),dict(name='Рынок',pos=[-300,200]),dict(name='Парк',pos=[250,250])]),
    dict(id='factory', name='Завод «Прогресс»', kind='industrial', desc='Промзона: цеха, склады, краны, трубы, гаражи, ж/д ветка. Тесно и громко.',
         raidMinutes=30, enemies=[10,18], lootDensity=1.2, bossChance=0.4, size=500,
         extracts=[dict(name='Ж/д ворота', pos=[-230,-230], cond=''),dict(name='Проходная', pos=[230,-230], cond=''),dict(name='Дыра в заборе (платно, 3000)', pos=[230,230], cond='money:3000'),dict(name='Кабинет директора (ключ)', pos=[0,80], cond='key_factory_director')],
         pois=[dict(name='Сборочный цех',pos=[0,0]),dict(name='Склад готовой продукции',pos=[-150,100]),dict(name='Котельная',pos=[150,100]),dict(name='Гаражи',pos=[-150,-120]),dict(name='Администрация',pos=[0,80])]),
    dict(id='suburb', name='Посёлок Сосновка', kind='suburb', desc='Дачный посёлок у леса: коттеджи, гаражи, магазин, озеро, лесная дорога. Снайперские дистанции.',
         raidMinutes=40, enemies=[8,14], lootDensity=0.9, bossChance=0.3, size=800,
         extracts=[dict(name='Лесная дорога', pos=[-380,380], cond=''),dict(name='Шоссе', pos=[380,-380], cond=''),dict(name='Лодка на озере', pos=[300,300], cond=''),dict(name='Гараж (ключ)', pos=[-120,-200], cond='key_suburb_garage'),dict(name='Вездеход (только вдвоём)', pos=[-380,-380], cond='coop')],
         pois=[dict(name='Магазин',pos=[0,0]),dict(name='Коттедж богача',pos=[150,-150]),dict(name='Озеро',pos=[300,250]),dict(name='Лесничество',pos=[-300,300]),dict(name='Старая церковь',pos=[-150,100])]),
    dict(id='mall', name='ТЦ «Орбита»', kind='mall', desc='Трёхэтажный торговый центр: магазины, фудкорт, кинотеатр, парковка. Тьма углов и эскалаторов.',
         raidMinutes=30, enemies=[12,20], lootDensity=1.4, bossChance=0.4, size=260,
         extracts=[dict(name='Главный вход', pos=[0,-125], cond=''),dict(name='Подземная парковка', pos=[-110,110], cond=''),dict(name='Служебный выход (ключ охраны)', pos=[110,110], cond='key_mall_security'),dict(name='Крыша (только вдвоём)', pos=[0,0], cond='coop')],
         pois=[dict(name='Фудкорт',pos=[0,60]),dict(name='Ювелирный',pos=[-60,-40]),dict(name='Электроника',pos=[60,-40]),dict(name='Кинотеатр',pos=[0,-80]),dict(name='Супермаркет',pos=[-80,60])]),
    dict(id='metro', name='Подземка', kind='metro', desc='Тоннели, станции, бункер и техпомещения. Нет света, нет неба, есть ночные.',
         raidMinutes=30, enemies=[12,22], lootDensity=1.3, bossChance=0.45, size=400,
         extracts=[dict(name='Станция «Северная»', pos=[0,-190], cond=''),dict(name='Вентшахта', pos=[180,100], cond=''),dict(name='Дрезина (платно, 6000)', pos=[-180,100], cond='money:6000'),dict(name='Диспетчерская (ключ)', pos=[0,0], cond='key_metro_control')],
         pois=[dict(name='Центральный зал',pos=[0,0]),dict(name='Бункер',pos=[-120,-60]),dict(name='Депо',pos=[120,60]),dict(name='Оружейная',pos=[-80,80])]),
]

# ---------------------------------------------------------------- КВЕСТЫ
quests = []
def quest(id, trader, name, desc, objectives, rewards, requires=None, minLevel=1, map_=None):
    quests.append(dict(id=id, trader=trader, name=name, desc=desc, objectives=objectives, rewards=rewards, requires=requires or [], minLevel=minLevel, map=map_))
def kill(n, **k): return dict(type='kill', count=n, **k)
def hand(itm, n): return dict(type='handover', item=itm, count=n)
def find(itm, n): return dict(type='find', item=itm, count=n)   # найти в рейде и вынести
def visit(mp, poi): return dict(type='visit', map=mp, poi=poi)
def extract(mp, n, **k): return dict(type='extract', map=mp, count=n, **k)
def skl(sid, n): return dict(type='skill', skill=sid, level=n)
def plant(mp, poi, itm): return dict(type='plant', map=mp, poi=poi, item=itm)
def rew(xp, money, rep=None, items_=None, unlock=None): return dict(xp=xp, money=money, rep=rep or {}, items=items_ or {}, unlock=unlock or [])
# Механик
quest('mech_1','mech','Проверка связи','Семёныч хочет убедиться, что ты не бесполезен. Убей троих мародёров на любой карте.',[kill(3, target='scav')],rew(800,8000,{'mech':0.03},{'ammo_9x19_std':60}))
quest('mech_2','mech','Бронзовый ствол','Принеси два пистолета — любых. Семёныч их переберёт.',[hand('*pistol',2)],rew(1200,10000,{'mech':0.04},{'g17':1}),['mech_1'])
quest('mech_3','mech','Тихо-тихо','Убей 8 врагов с глушителем на оружии.',[kill(8, suppressed=True)],rew(2500,20000,{'mech':0.05},{'supp_pist9_1':1}),['mech_2'],3)
quest('mech_4','mech','Запчасти','Нужны болты, гайки и WD-40. Найди на заводе.',[hand('bolts',5),hand('nuts',5),hand('wd40',2)],rew(2200,18000,{'mech':0.05},{'toolset':1}),['mech_2'],3)
quest('mech_5','mech','Калашников','Убей 15 врагов из оружия семейства АК.',[kill(15, family='ak')],rew(4000,35000,{'mech':0.06},{'ak74m':1,'ammo_5.45x39_std':120}),['mech_3'],5)
quest('mech_6','mech','Инструменталка','На заводе есть инструменталка. Ключ где-то у бригадира. Вынеси набор инструментов оттуда.',[visit('factory','Сборочный цех'),find('toolset',1)],rew(5000,40000,{'mech':0.08},{'barrel_ak_long':1}),['mech_4'],6,'factory')
quest('mech_7','mech','Снайперская пара','Убей 10 врагов с дистанции 80+ метров.',[kill(10, minDist=80)],rew(6000,50000,{'mech':0.08},{'scope_pso':1}),['mech_5'],8)
quest('mech_8','mech','Чистота ствола','Отремонтируй 5 единиц оружия на верстаке и прокачай «Уход за оружием» до 10.',[skl('maintenance',10)],rew(7000,45000,{'mech':0.1},{'repair_kit':2}),['mech_6'],10)
quest('mech_9','mech','Коллекционер','Принеси по одной винтовке: M4A1, HK416, SCAR-L.',[hand('m4a1',1),hand('hk416',1),hand('scarl',1)],rew(12000,150000,{'mech':0.15},{'mdr':1}),['mech_7','mech_8'],15)
quest('mech_10','mech','Мастер-оружейник','Убей 30 врагов из оружия, у которого занято 5+ слотов модификаций.',[kill(30, minMods=5)],rew(20000,250000,{'mech':0.2},{'an94':1,'scope_elcan':1}),['mech_9'],20)
# Доктор
quest('doc_1','doc','Первая помощь','Принеси 5 бинтов. Их всегда не хватает.',[hand('bandage',5)],rew(600,6000,{'doc':0.03},{'ai2':2}))
quest('doc_2','doc','Жгуты и шины','Собери 3 жгута и 3 шины.',[hand('tourniquet',3),hand('splint',3)],rew(1500,12000,{'doc':0.05},{'salewa':1}),['doc_1'],2)
quest('doc_3','doc','Аптека на проспекте','В Донецке есть аптека. Вынеси оттуда IFAK.',[visit('donetsk','Площадь Ленина'),find('ifak',1)],rew(3000,25000,{'doc':0.06},{'key_donetsk_pharmacy':1}),['doc_2'],4,'donetsk')
quest('doc_4','doc','Выживи','Выйди живым из 5 рейдов подряд без смерти.',[extract('*',5, streak=True)],rew(4000,30000,{'doc':0.06},{'grizzly':1}),['doc_2'],5)
quest('doc_5','doc','Хирург','Прокачай навык «Здоровье» до 10 и принеси хирургический набор.',[skl('health',10),hand('surgkit',1)],rew(6000,45000,{'doc':0.1},{'cms':2,'morphine':3}),['doc_3','doc_4'],8)
quest('doc_6','doc','Ночные','Ночные не люди. Убей 6 Ночных в подземке.',[kill(6, target='cultist', map='metro')],rew(9000,70000,{'doc':0.12},{'adrenaline':5}),['doc_5'],12,'metro')
quest('doc_7','doc','Пациент','Вылечи сопартийца 20 раз (аптечкой по чужому телу).',[dict(type='heal_ally', count=20)],rew(8000,60000,{'doc':0.12},{'ifak':3}),['doc_5'],10)
quest('doc_8','doc','Главврач','Выйди из рейда с 30 предметами медицины за всё время.',[find('*med',30)],rew(15000,120000,{'doc':0.2},{'secure_case':1}),['doc_6'],15)
# Барыга
quest('fence_1','fence','Мелочёвка','Принеси 3 ценных предмета. Любых.',[hand('*valuable',3)],rew(800,10000,{'fence':0.04}))
quest('fence_2','fence','Электроника','Гоша любит процессоры и память. 2 процессора и 3 модуля памяти.',[hand('cpu',2),hand('ram',3)],rew(2500,30000,{'fence':0.06},{'key_mirea_lab':1}),['fence_1'],3)
quest('fence_3','fence','Серверная МИРЭА','В серверной МИРЭА стоят видеокарты. Вынеси одну.',[visit('mirea','Серверная'),find('gpu',1)],rew(6000,80000,{'fence':0.1},{'key_mirea_server':1}),['fence_2'],6,'mirea')
quest('fence_4','fence','Ювелирка','В ТЦ «Орбита» есть ювелирный. Принеси 3 кольца и цепь.',[visit('mall','Ювелирный'),find('ring',3),find('gold_chain',1)],rew(7000,90000,{'fence':0.1},{'key_mall_jewelry':1}),['fence_3'],8,'mall')
quest('fence_5','fence','Банк','Хранилище банка в Донецке. Ключ Гоша даст. Принеси оттуда биткоин.',[find('bitcoin',1)],rew(15000,200000,{'fence':0.15},{'armor_gzhel':1}),['fence_4'],12,'donetsk')
quest('fence_6','fence','Чёрный рынок','Продай Гоше предметов на 500 000.',[dict(type='sell', trader='fence', amount=500000)],rew(12000,100000,{'fence':0.2},{'secure_case_big':1}),['fence_5'],15)
quest('fence_7','fence','Зачётка','Говорят, в деканате лежит зачётка с одними пятёрками. Гоше она нужна — не спрашивай зачем.',[find('zachetka',1)],rew(5000,60000,{'fence':0.08},{'key_mirea_dean':1}),['fence_3'],7,'mirea')
# Комендант
quest('kom_1','komendant','Присяга','Убей 5 рейдеров.',[kill(5, target='raider')],rew(1500,12000,{'komendant':0.04},{'armor_6b2':1}))
quest('kom_2','komendant','Разведка боем','Побывай у всех точек интереса на заводе.',[visit('factory','Сборочный цех'),visit('factory','Склад готовой продукции'),visit('factory','Котельная'),visit('factory','Гаражи')],rew(2500,20000,{'komendant':0.05},{'gren_rgd5':3}),['kom_1'],3,'factory')
quest('kom_3','komendant','Головы','20 убийств в голову.',[kill(20, headshot=True)],rew(4500,35000,{'komendant':0.06},{'helmet_6b47':1}),['kom_1'],5)
quest('kom_4','komendant','Бригадир','Убей Бригадира на заводе.',[kill(1, target='boss_brigadir')],rew(8000,80000,{'komendant':0.1},{'rpk16':1}),['kom_2','kom_3'],8,'factory')
quest('kom_5','komendant','Завхоз','Завхоз в МИРЭА держит кафедру в страхе. Убей его и принеси ключ от склада.',[kill(1, target='boss_zavhoz'),hand('key_mirea_storage',1)],rew(9000,90000,{'komendant':0.1},{'ak12':1}),['kom_4'],10,'mirea')
quest('kom_6','komendant','Гранатомётчик','Убей 10 врагов гранатами.',[kill(10, grenade=True)],rew(6000,50000,{'komendant':0.08},{'gren_f1':5}),['kom_3'],8)
quest('kom_7','komendant','Шахтёр','Шахтёр в Донецке — ходячий танк. Собери отряд и убей его.',[kill(1, target='boss_shakhtyor')],rew(15000,150000,{'komendant':0.15},{'armor_6b43':1}),['kom_5'],14,'donetsk')
quest('kom_8','komendant','Машинист','Машинист в Подземке. Тяжелее не бывает.',[kill(1, target='boss_mashinist')],rew(20000,250000,{'komendant':0.2},{'pkp':1,'helmet_altyn':1}),['kom_7'],18,'metro')
quest('kom_9','komendant','Пулемётчик','Убей 40 врагов из пулемёта.',[kill(40, cls='lmg')],rew(12000,120000,{'komendant':0.12},{'mag_int545_drum':1}),['kom_4'],12)
quest('kom_10','komendant','Снайпер','Убей 25 врагов из снайперской винтовки с дистанции 120+.',[kill(25, cls='sniper', minDist=120)],rew(14000,140000,{'komendant':0.15},{'sv98':1,'scope_10x':1}),['kom_3'],12)
# Бабуля
quest('bab_1','babulya','Продукты','Принеси тушёнку и хлеб. Три и три.',[hand('tushenka',3),hand('bread',3)],rew(500,5000,{'babulya':0.04},{'bag_school':1}))
quest('bab_2','babulya','Чебуреки','В столовой МИРЭА чебуреки. Вынеси 4.',[visit('mirea','Столовая'),find('cheburek',4)],rew(1500,10000,{'babulya':0.05},{'rig_scav':1}),['bab_1'],2,'mirea')
quest('bab_3','babulya','Дача','Посёлок Сосновка. Найди коттедж богача и принеси оттуда виски.',[visit('suburb','Коттедж богача'),find('whiskey',1)],rew(2500,18000,{'babulya':0.06},{'key_suburb_cottage':1}),['bab_2'],4,'suburb')
quest('bab_4','babulya','Сигареты','5 блоков сигарет.',[hand('cigs',5)],rew(2000,15000,{'babulya':0.05},{'bag_pilgrim':1}),['bab_1'],4)
quest('bab_5','babulya','Лесник','Лесник обстреливает дачников. Разберись.',[kill(1, target='boss_lesnik')],rew(7000,60000,{'babulya':0.1},{'mosin':1,'scope_3_9':1}),['bab_3'],9,'suburb')
quest('bab_6','babulya','Рынок','Побывай на рынке в Донецке и принеси 3 пачки чая... ладно, 3 сока.',[visit('donetsk','Рынок'),find('juice',3)],rew(3000,20000,{'babulya':0.06},{'mre':3}),['bab_3'],6,'donetsk')
quest('bab_7','babulya','Учебники','Принеси 6 учебников по матану. Внуку.',[hand('textbook',6)],rew(4000,25000,{'babulya':0.08},{'bag_attack':1}),['bab_4'],7)
quest('bab_8','babulya','Хозяйка','Прокачай «Метаболизм» до 15 и «Выносливость» до 15.',[skl('metabolism',15),skl('endurance',15)],rew(8000,50000,{'babulya':0.15},{'bag_expedition':1}),['bab_6','bab_7'],12)
# Хакер
quest('hack_1','hacker','Флешка','Найди флешку с данными. Она в деканате или серверной МИРЭА.',[find('flash_drive',1)],rew(2000,20000,{'hacker':0.05},{'laser_red':1}),[],3,'mirea')
quest('hack_2','hacker','Железо','3 SSD и ноутбук.',[hand('ssd',3),hand('laptop',1)],rew(3500,35000,{'hacker':0.07},{'rds_eotech':1}),['hack_1'],5)
quest('hack_3','hacker','Маяк','Установи пауэрбанк на крыше ТЦ «Орбита» (поднимись и используй).',[plant('mall','Фудкорт','powerbank')],rew(5000,40000,{'hacker':0.08},{'scope_acog':1}),['hack_2'],7,'mall')
quest('hack_4','hacker','Разведданные','Собери 5 папок разведданных.',[find('intel',5)],rew(7000,70000,{'hacker':0.1},{'nvg':1}),['hack_2'],9)
quest('hack_5','hacker','Диспетчерская','В подземке есть диспетчерская. Поставь там ноутбук.',[plant('metro','Центральный зал','laptop')],rew(9000,90000,{'hacker':0.12},{'key_metro_control':1}),['hack_3','hack_4'],12,'metro')
quest('hack_6','hacker','Степаныч','Охранник ТЦ мешает бизнесу. Убери его.',[kill(1, target='boss_stepanych')],rew(12000,120000,{'hacker':0.15},{'p90':1,'scope_vudu':1}),['hack_3'],12,'mall')
quest('hack_7','hacker','Сеть','Побывай у диспетчерской, в серверной МИРЭА и в офисе в Донецке за один рейд каждый.',[visit('metro','Центральный зал'),visit('mirea','Серверная'),visit('donetsk','Банк')],rew(15000,150000,{'hacker':0.2},{'t5000':1,'scope_12x':1}),['hack_5','hack_6'],16)
quest('hack_8','hacker','Интеллект','Прокачай «Интеллект» до 20.',[skl('intellect',20)],rew(10000,80000,{'hacker':0.12},{'laser_peq':1}),['hack_4'],14)
# разведка каждой карты (генерация)
for m in maps:
    pois = [p['name'] for p in m['pois']]
    quest(f'scout_{m["id"]}_1', 'komendant', f'Разведка: {m["name"]}', f'Обойди ключевые точки на карте «{m["name"]}».', [visit(m['id'], p) for p in pois[:3]], rew(1500, 10000, {'komendant':0.03}), [], 2, m['id'])
    quest(f'scout_{m["id"]}_2', 'komendant', f'Зачистка: {m["name"]}', f'Убей 12 врагов на карте «{m["name"]}».', [kill(12, map=m['id'])], rew(3000, 25000, {'komendant':0.04}), [f'scout_{m["id"]}_1'], 4, m['id'])
    quest(f'scout_{m["id"]}_3', 'fence', f'Вынос: {m["name"]}', f'Выйди с карты «{m["name"]}» 3 раза с лутом дороже 50 000.', [extract(m['id'], 3, minLoot=50000)], rew(4000, 40000, {'fence':0.05}), [f'scout_{m["id"]}_1'], 5, m['id'])

# ---------------------------------------------------------------- ВЫВОД
dump('weapons', weapons); dump('mods', mods); dump('ammo', ammo); dump('items', items)
dump('skills', dict(skills=skills, perks=perks, branches=branches)); dump('hideout', dict(modules=hideout, recipes=recipes))
dump('enemies', enemies); dump('maps', maps); dump('quests', quests); dump('traders', TRADERS)
print('OK ->', OUT)
