"""Office-site geometry, constructed exclusively through MCP tools (metres)."""
import math
from office_demo_entourage import tree_canopy, person_profile

LAYER = 'Demo_OfficePark'
CAMERA = [49, -68, 7]
TARGET = [10, 5, 6]
COLORS = {'Site': '#F7F7F2', 'Paving': '#E2E6E3', 'Parking': '#C9D2D1',
          'Masonry': '#E5E3DC', 'Glass': '#D2E9E8', 'SideGlass': '#BFDAD9',
          'Frame': '#53615E', 'Trees': '#E4EEEA', 'People': '#FAFBF6',
          'Lines': '#F8F8F1', 'Trunk': '#C5CEC6', 'Planting': '#ABBFA8'}


class OfficeScene:
    def __init__(self, call):
        self.call = call
        self.billboards = []

    def layers(self):
        for name, color in COLORS.items():
            full = LAYER + '::' + name
            self.call('create_layer', {'name': full, 'color': color})
            self.call('set_layer_props', {'name': full, 'color': color, 'visible': True})

    def box(self, low, high, layer, name):
        args = dict(zip(('minX', 'minY', 'minZ', 'maxX', 'maxY', 'maxZ'), low + high))
        args.update(layer=LAYER + '::' + layer, name=name)
        return self.call('create_box', args)

    def line(self, points, name):
        return self.call('create_polyline', {'points': points, 'layer': LAYER + '::Frame', 'name': name})

    def site(self):
        self.box([-1000, -1000, -.18], [1000, 1000, -.12], 'Site', 'ground')
        self.box([-17, -5, -.1], [36, 23, 0], 'Paving', 'site')
        self.box([-17, -5, 0], [36, -2, .15], 'Site', 'sidewalk')
        self.box([-17, -5.12, -.02], [36, -5, .16], 'Masonry', 'curb')
        self.box([23, -1, .01], [35, 21, .04], 'Parking', 'parking lot')

    def parking(self):
        for y in range(0, 20, 3):
            self.box([28.8, y, .045], [34.5, y + .08, .055], 'Lines', 'parking stripe')
        self.box([28.8, 0, .045], [28.9, 18, .055], 'Lines', 'parking aisle')
        for y in (2, 8, 14):
            self.box([30, y, .07], [34, y+1.7, .8], 'Masonry', 'parked car')
            self.box([30.9, y+.15, .8], [33, y+1.55, 1.35], 'SideGlass', 'car cabin')

    def massing(self, story):
        z = story * 3.3
        self.box([0, 1, z], [20, 16, z+.18], 'Masonry', 'floor slab')
        self.box([.3, 15.4, z+.18], [19.7, 16, z+3.3], 'Masonry', 'rear wall')
        for x in (.35, 7, 13, 19.65):
            self.box([x, 1.4, z+.18], [x+.22, 1.62, z+3.3], 'Masonry', 'column')
        if story == 3:
            self.box([0, 1, 13.2], [20, 16, 13.4], 'Masonry', 'roof')

    def curtain(self, story):
        level = 'Demo_L' + str(story)
        self.call('create_level', {'name': level, 'elevation': story*3.3, 'height': 3.3})
        for path, layer in (([[0, 1], [20, 1]], 'Glass'), ([[20, 1], [20, 16]], 'SideGlass'),
                            ([[0, 16], [0, 1]], 'SideGlass')):
            result = self.call('create_curtain_wall', {'path': path, 'height': 3.3,
                               'mullion_spacing': 2, 'level': level, 'name': 'demo facade'})
            for key, dest in (('frame_ids', 'Frame'), ('glass_ids', layer)):
                ids = result[key]
                if not ids:
                    raise RuntimeError('Curtain wall returned no ' + key)
                self.call('set_object_layer', {'ids': ','.join(ids), 'layer': LAYER+'::'+dest})

    def entrance(self):
        self.box([7, -.4, 2.8], [13, 2, 3], 'Frame', 'entry canopy')
        for x in (7.2, 12.7):
            self.box([x, -.2, .15], [x+.08, -.12, 2.8], 'Frame', 'canopy post')
        self.line([[10, .9, .2], [10, .9, 2.7]], 'door meeting stile')
        self.box([-2, -.9, .15], [5, .1, .45], 'Masonry', 'planter')
        self.box([-1.9, -.8, .45], [4.9, 0, .8], 'Planting', 'low planting')

    def context(self):
        self.box([-14, 4, 0], [-2, 18, 9.6], 'Masonry', 'neighbor')
        for z in (1.2, 4.2, 7.2):
            for x in (-12, -9, -6):
                self.box([x, 3.94, z], [x+1.25, 4, z+1.7], 'SideGlass', 'neighbor window')
        self.box([-14.2, 3.9, 9.6], [-1.8, 18.1, 9.8], 'Masonry', 'neighbor cornice')

    def silhouette(self, points, origin, layer, name):
        x, y, z = origin
        dx, dy = CAMERA[0]-x, CAMERA[1]-y
        length = math.hypot(dx, dy)
        right = (-dy/length, dx/length)
        world = [[x+u*right[0], y+u*right[1], z+v] for u, v in points]
        curve = self.call('create_polyline', {'points': world, 'closed': True,
                          'layer': LAYER+'::Frame', 'name': name+' outline'})
        fill = self.call('create_planar_surface', {'ids': [curve['id']], 'keep_inputs': False})
        ids = fill.get('ids') or [fill['id']]
        self.call('set_object_layer', {'ids': ','.join(ids), 'layer': LAYER+'::'+layer})
        self.call('set_object_name', {'ids': ','.join(ids), 'name': name})
        self.billboards.append({'ids': ids, 'origin': origin, 'angle': math.atan2(dy, dx)})

    def tree(self, index):
        x, y = [(-11, -1.5), (-5, -2), (2, -2), (17.5, -1.5), (24, 8), (24, 17)][index]
        points = tree_canopy(index)
        self.silhouette(points, (x, y, .15), 'Trees', 'tree canopy')
        self.silhouette([(-.065, 0), (.075, 0), (.06, 1.7), (.29, 2.75),
                         (.20, 2.8), (-.015, 2.02), (-.19, 3.1), (-.26, 3.12), (-.075, 1.65)],
                        (x, y-.04, .15), 'Trunk', 'tree trunk')

    def person(self, index):
        x, y = [(5.5, -3), (11, -2.6), (14, -3.5), (-7, -3.2)][index]
        points = person_profile(index)
        self.silhouette(points, (x,y,.16), 'People', 'pedestrian')


    def face_camera(self, camera):
        for item in self.billboards:
            x, y, _ = item['origin']
            angle = math.atan2(camera[1]-y, camera[0]-x)
            delta = math.degrees(angle-item['angle'])
            if abs(delta) > .0001:
                self.call('transform_objects', {'ids': item['ids'], 'op': 'rotate',
                          'angle_deg': delta, 'axis': [0,0,1], 'center': item['origin'], 'copy': False})
            item['angle'] = angle
