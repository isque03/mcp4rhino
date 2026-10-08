"""Deterministic architectural entourage profiles in a local vertical plane."""
import math
import random


def soften(points, passes=1):
    """Round polygon corners without overshooting or introducing spline loops."""
    for _ in range(passes):
        result = []
        for a, b in zip(points, points[1:] + points[:1]):
            result.extend([(a[0]*.8+b[0]*.2, a[1]*.8+b[1]*.2),
                           (a[0]*.2+b[0]*.8, a[1]*.2+b[1]*.8)])
        points = result
    return points


def tree_canopy(seed):
    rng = random.Random(seed)
    clusters = []
    for i in range(34):
        a = i * math.tau / 34
        spread = rng.uniform(.82, 1.02)
        clusters.append((1.48*spread*math.cos(a), 2.18*spread*math.sin(a),
                         rng.uniform(.18, .45)))
    points = []
    for i in range(360):
        a = i * math.tau / 360
        dx, dy = math.cos(a), math.sin(a)
        radius = 1 / math.sqrt((dx/1.15)**2 + (dy/1.8)**2)
        for x,y,r in clusters:
            along = x*dx+y*dy
            discriminant = r*r - (x*x+y*y-along*along)
            if discriminant >= 0:
                radius = max(radius, along+math.sqrt(discriminant))
        points.append((radius*dx, 4.25+radius*dy))
    return soften(points)


WALKING = [(-.065,1.51),(-.10,1.57),(-.12,1.66),(-.10,1.75),(-.04,1.8),
           (.055,1.79),(.11,1.72),(.10,1.63),(.07,1.57),(.065,1.51),
           (.17,1.46),(.22,1.28),(.38,1.14),(.39,1.08),(.34,1.06),
           (.16,1.21),(.12,1.12),(.19,.92),(.27,.67),(.38,.40),(.49,.08),
           (.58,.035),(.56,0),(.40,0),(.29,.34),(.11,.60),(.015,.78),
           (-.06,.54),(-.25,.31),(-.40,.065),(-.36,.02),(-.38,0),(-.52,0),
           (-.53,.06),(-.37,.39),(-.18,.70),(-.16,.89),(-.12,1.13),
           (-.17,1.3),(-.24,1.13),(-.40,.99),(-.45,1.02),(-.43,1.09),
           (-.29,1.22),(-.23,1.43),(-.14,1.49)]
STANDING = [(-.065,1.51),(-.11,1.59),(-.12,1.69),(-.075,1.78),(.025,1.81),
            (.105,1.75),(.12,1.65),(.075,1.55),(.07,1.51),(.20,1.46),
            (.24,1.30),(.27,1.08),(.25,.91),(.20,.9),(.19,1.1),(.14,1.28),
            (.13,1.05),(.18,.89),(.14,.58),(.13,.12),(.20,.055),(.20,.02),
            (.055,.02),(.01,.57),(-.025,.78),(-.09,.48),(-.16,.045),
            (-.10,.025),(-.13,0),(-.26,0),(-.20,.52),(-.20,.87),
            (-.15,1.09),(-.17,1.29),(-.22,1.06),(-.23,.91),(-.28,.91),
            (-.31,1.08),(-.27,1.37),(-.21,1.46)]


def person_profile(index):
    source = WALKING if index % 2 == 0 else STANDING
    facing = -1 if index == 2 else 1
    scale = [1.08, 1, 1.12, .96][index]
    return [(x*facing*scale, y*scale) for x,y in soften(source, 2)]


def flight_camera(step, count, camera, target):
    """Ease into a rising 75-degree orbit across the two front corners."""
    t = step / (count-1)
    eased = t*t*(3-2*t)
    dx, dy = camera[0]-target[0], camera[1]-target[1]
    angle = math.atan2(dy, dx) - math.radians(75)*eased
    radius = math.hypot(dx, dy) * (1-.15*eased)
    return [target[0]+radius*math.cos(angle), target[1]+radius*math.sin(angle),
            camera[2]+23*eased]
