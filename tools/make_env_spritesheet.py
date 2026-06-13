"""
Generates labeled spritesheets for RetroLOTR:
  - environmental_cards_spritesheet.png   (57 unique, each emblem distinct)
  - terrain_spritesheet.png               (11 terrain types)
  - terrain_features_spritesheet.png      (15 hex features)

Every environmental card has its own bespoke emblem; the nine "... Unleashed"
Nazgul intentionally share one hooded-wraith silhouette.

Run:  python tools/make_env_spritesheet.py
"""
import json
import math
import os
import random
from PIL import Image, ImageDraw, ImageFont, ImageChops

# ----------------------------------------------------------------------------
# palette
# ----------------------------------------------------------------------------
GOLD=(240,200,90); AMBER=(235,150,60); RED=(210,70,55); PINK=(235,160,175)
SILVER=(205,215,235); BLUE=(110,170,230); ICE=(170,215,240); GREY=(165,170,182)
GREEN=(120,185,110); LEAF=(150,200,120); TAN=(212,182,120); PURPLE=(155,115,190)
SMOKE=(120,140,120); BROWN=(165,130,90); BONE=(222,217,202); SKY=(120,170,215)
SAND=(228,202,140); WATER=(70,135,205); DWATER=(45,85,150); SWAMP=(100,125,85)
STONE=(150,152,162); LAVA=(232,110,40); MUD=(140,108,72); WHITE=(245,245,245)
GHOST=(185,212,228); NIGHT=(60,60,95)

def lighten(c,f): return tuple(min(255,int(v+(255-v)*f)) for v in c)
def darken(c,f):  return tuple(max(0,int(v*(1-f))) for v in c)

# safe primitives (auto-sorted boxes)
def E(d,x0,y0,x1,y1,**k): d.ellipse([min(x0,x1),min(y0,y1),max(x0,x1),max(y0,y1)],**k)
def R(d,x0,y0,x1,y1,**k): d.rectangle([min(x0,x1),min(y0,y1),max(x0,x1),max(y0,y1)],**k)

def starpts(cx,cy,r,n,inner=0.4,rot=-math.pi/2):
    p=[]
    for i in range(n*2):
        rad=r if i%2==0 else r*inner
        a=rot+i*math.pi/n
        p.append((cx+rad*math.cos(a),cy+rad*math.sin(a)))
    return p

def grad_disc(img,cx,cy,r,top,bot):
    n=int(2*r); tile=Image.new("RGBA",(n,n),(0,0,0,0)); td=ImageDraw.Draw(tile)
    for i in range(n):
        t=i/n; col=tuple(int(top[j]+(bot[j]-top[j])*t) for j in range(3))
        td.line([0,i,n,i],fill=col+(255,))
    m=Image.new("L",(n,n),0); ImageDraw.Draw(m).ellipse([0,0,n-1,n-1],fill=255)
    tile.putalpha(m)
    img.alpha_composite(tile,(int(cx-r),int(cy-r)))

def punch_ellipse(img,x0,y0,x1,y1):
    """Erase (make transparent) an elliptical region of an RGBA sprite tile."""
    m=Image.new("L",img.size,0)
    ImageDraw.Draw(m).ellipse([min(x0,x1),min(y0,y1),max(x0,x1),max(y0,y1)],fill=255)
    img.putalpha(ImageChops.subtract(img.getchannel("A"),m))

def cloud(d,cx,cy,r,col):
    for dx,dy,s in [(-.55,.1,.5),(0,-.2,.62),(.55,.1,.5),(0,.25,.7)]:
        E(d,cx+dx*r-s*r,cy+dy*r-s*r,cx+dx*r+s*r,cy+dy*r+s*r,fill=col)

def peak(d,cx,base,w,h,col,snow=True):
    apex=(cx,base-h)
    d.polygon([(cx-w,base),apex,(cx+w,base)],fill=col)
    if snow:
        f=0.32
        d.polygon([(cx-w*f,base-h*(1-f)),apex,(cx+w*f,base-h*(1-f))],fill=WHITE)

def pine(d,x,base,h,col):
    w=h*0.42
    R(d,x-3,base-2,x+3,base+h*0.12,fill=BROWN)
    for k in range(3):
        yy=base-h*(0.15+0.28*k); ww=w*(1-0.22*k)
        d.polygon([(x-ww,yy),(x,yy-h*0.32),(x+ww,yy)],fill=darken(col,0.06*k))

def flame(d,cx,cy,r,col):
    d.polygon([(cx,cy-r),(cx+r*.7,cy+r*.3),(cx+r*.45,cy+r*.75),
               (cx,cy+r*.9),(cx-r*.45,cy+r*.75),(cx-r*.7,cy+r*.3)],fill=col)
    d.polygon([(cx,cy-r*.3),(cx+r*.35,cy+r*.35),(cx,cy+r*.7),(cx-r*.35,cy+r*.35)],
              fill=lighten(col,0.45))

# ============================================================================
# ENVIRONMENTAL EMBLEMS  (fn(img,d,cx,cy,r))
# ============================================================================
def ec_sun(img,d,cx,cy,r):
    for i in range(12):
        a=i*math.pi/6
        d.line([cx+.58*r*math.cos(a),cy+.58*r*math.sin(a),cx+1.05*r*math.cos(a),cy+1.05*r*math.sin(a)],fill=GOLD,width=5)
    E(d,cx-.6*r,cy-.6*r,cx+.6*r,cy+.6*r,fill=GOLD); E(d,cx-.42*r,cy-.42*r,cx+.42*r,cy+.42*r,fill=lighten(GOLD,.4))

def ec_red_sun(img,d,cx,cy,r):
    E(d,cx-r,cy-r,cx+r,cy+r,fill=darken(RED,.35))
    E(d,cx-.78*r,cy-.78*r,cx+.78*r,cy+.78*r,fill=RED)
    E(d,cx-.5*r,cy-.5*r,cx+.5*r,cy+.5*r,fill=darken(RED,.45))
    for yy in (-.3,0,.3):
        d.line([cx-1.1*r,cy+yy*r,cx-.85*r,cy+yy*r],fill=lighten(RED,.2),width=4)
        d.line([cx+.85*r,cy+yy*r,cx+1.1*r,cy+yy*r],fill=lighten(RED,.2),width=4)

def ec_cloudless(img,d,cx,cy,r):
    grad_disc(img,cx,cy,r,(150,200,235),(205,230,245))
    sx,sy=cx-.4*r,cy-.42*r
    for i in range(8):
        a=i*math.pi/4
        d.line([sx+.3*r*math.cos(a),sy+.3*r*math.sin(a),sx+.5*r*math.cos(a),sy+.5*r*math.sin(a)],fill=GOLD,width=4)
    E(d,sx-.28*r,sy-.28*r,sx+.28*r,sy+.28*r,fill=GOLD)
    for bx,by in [(cx+.15*r,cy+.1*r),(cx+.5*r,cy+.3*r)]:
        d.line([bx-10,by,bx,by-6],fill=WHITE,width=3); d.line([bx,by-6,bx+10,by],fill=WHITE,width=3)

def ec_gates_morning(img,d,cx,cy,r):
    R(d,cx-r,cy+.5*r,cx+r,cy+.8*r,fill=darken(AMBER,.4))   # ground
    E(d,cx-.55*r,cy-.1*r,cx+.55*r,cy+1*r,fill=AMBER)
    R(d,cx-r,cy+.55*r,cx+r,cy+1.1*r,fill=darken(AMBER,.4))
    for px in (-.85,.85):
        R(d,cx+px*r-9,cy-.9*r,cx+px*r+9,cy+.55*r,fill=STONE)
    R(d,cx-r,cy-1*r,cx+r,cy-.78*r,fill=STONE)

def ec_dawn(img,d,cx,cy,r):
    grad_disc(img,cx,cy,r,(245,180,170),(250,225,180))
    for i in range(7):
        a=math.pi+i*math.pi/6
        d.line([cx,cy+.35*r,cx+r*math.cos(a),cy+.35*r+r*math.sin(a)],fill=lighten(PINK,.1),width=3)
    E(d,cx-.5*r,cy-.15*r,cx+.5*r,cy+.85*r,fill=GOLD)
    R(d,cx-r,cy+.35*r,cx+r,cy+r,fill=(120,90,110))

def ec_first_light(img,d,cx,cy,r):
    d.polygon([(cx,cy-r),(cx+.5*r,cy+r),(cx-.5*r,cy+r)],fill=lighten(GOLD,.25))
    d.polygon([(cx,cy-r),(cx+.2*r,cy+r),(cx-.2*r,cy+r)],fill=lighten(GOLD,.55))
    for k in range(3):
        E(d,cx-.7*r+k*.5*r-5,cy+.55*r-5,cx-.7*r+k*.5*r+5,cy+.55*r+5,fill=GOLD)

def ec_light_cloud(img,d,cx,cy,r):
    E(d,cx-.45*r,cy-.7*r,cx+.45*r,cy+.2*r,fill=GOLD)
    cloud(d,cx+.1*r,cy-.05*r,r*.9,GREY)
    for i in range(4):
        x=cx-.5*r+i*.35*r
        d.line([x,cy+.35*r,x-8,cy+.85*r],fill=lighten(GOLD,.2),width=4)

def ec_a_light(img,d,cx,cy,r):
    R(d,cx-.05*r,cy-1*r,cx+.05*r,cy-.7*r,fill=STONE)          # hook
    R(d,cx-.3*r,cy-.7*r,cx+.3*r,cy-.55*r,fill=darken(GOLD,.4))# top
    d.polygon([(cx-.3*r,cy-.55*r),(cx+.3*r,cy-.55*r),(cx+.4*r,cy+.6*r),(cx-.4*r,cy+.6*r)],fill=lighten(GOLD,.35))
    E(d,cx-.18*r,cy-.2*r,cx+.18*r,cy+.3*r,fill=WHITE)
    R(d,cx-.42*r,cy+.6*r,cx+.42*r,cy+.75*r,fill=darken(GOLD,.4))

def ec_moon(img,d,cx,cy,r):
    E(d,cx-r,cy-r,cx+r,cy+r,fill=SILVER); punch_ellipse(img,cx-.55*r,cy-r,cx+1.5*r,cy+r)

def ec_full_moon(img,d,cx,cy,r):
    E(d,cx-r,cy-r,cx+r,cy+r,fill=SILVER)
    for sx,sy,sr in [(-.3,-.2,.18),(.25,.1,.13),(.05,.4,.1),(-.45,.3,.08)]:
        E(d,cx+sx*r-sr*r,cy+sy*r-sr*r,cx+sx*r+sr*r,cy+sy*r+sr*r,fill=darken(SILVER,.16))

def ec_new_moon(img,d,cx,cy,r):
    E(d,cx-r,cy-r,cx+r,cy+r,outline=darken(SILVER,.3),width=5)
    E(d,cx-.62*r,cy-.62*r,cx+.62*r,cy+.62*r,outline=darken(SILVER,.5),width=2)

def ec_stars(img,d,cx,cy,r):
    random.seed(7)
    for _ in range(9):
        sx=cx+random.uniform(-r,r); sy=cy+random.uniform(-r,r)
        if math.hypot(sx-cx,sy-cy)>r: continue
        d.polygon(starpts(sx,sy,random.uniform(4,11),4,.3),fill=(235,235,200))

def ec_earendil(img,d,cx,cy,r):
    d.line([cx,cy-1.05*r,cx,cy+1.05*r],fill=lighten(GOLD,.4),width=2)
    d.line([cx-1.05*r,cy,cx+1.05*r,cy],fill=lighten(GOLD,.4),width=2)
    d.polygon(starpts(cx,cy,r,4,.14),fill=(245,245,210))
    d.polygon(starpts(cx,cy,.55*r,4,.2,rot=0),fill=WHITE)

def ec_dispatch(img,d,cx,cy,r):
    d.line([cx-r,cy+.7*r,cx+.3*r,cy-.4*r],fill=lighten(SILVER,.2),width=5)
    d.line([cx-.6*r,cy+.45*r,cx+.2*r,cy-.3*r],fill=SILVER,width=2)
    d.polygon(starpts(cx+.45*r,cy-.5*r,.45*r,4,.2),fill=(245,245,220))
    d.polygon(starpts(cx-.5*r,cy-.5*r,.16*r,4,.3),fill=SILVER)

def ec_faded_star(img,d,cx,cy,r):
    R(d,cx-r,cy+.4*r,cx+r,cy+r,fill=darken(WATER,.4))
    for yy in (.55,.78):
        d.line([cx-.8*r,cy+yy*r,cx+.8*r,cy+yy*r],fill=lighten(WATER,.1),width=3)
    d.polygon(starpts(cx,cy-.2*r,.45*r,4,.25),fill=(160,170,200))

def ec_rain(img,d,cx,cy,r):
    cloud(d,cx,cy-.25*r,r*.9,GREY)
    for i in range(5):
        x=cx-.6*r+i*.3*r
        d.line([x,cy+.25*r,x-7,cy+.8*r],fill=BLUE,width=4)

def ec_clouds(img,d,cx,cy,r):
    cloud(d,cx-.2*r,cy-.15*r,r*.8,lighten(GREY,.18))
    cloud(d,cx+.25*r,cy+.2*r,r*.85,GREY)

def ec_fog(img,d,cx,cy,r):
    for i in range(7):
        off=(i%2)*14-7; w=6 if i%2 else 5
        d.line([cx-r+off,cy-.75*r+i*.25*r,cx+r+off,cy-.75*r+i*.25*r],fill=lighten(GREY,.06+.06*(i%2)),width=w)

def ec_wind(img,d,cx,cy,r):
    for yy in (-.35,.05,.4):
        d.arc([cx-r,cy+yy*r-18,cx+.7*r,cy+yy*r+18],200,70,fill=lighten(SKY,.1),width=5)
    d.polygon([(cx+.55*r,cy-.05*r),(cx+.95*r,cy-.2*r),(cx+.85*r,cy+.2*r)],fill=LEAF)

def ec_lightning(img,d,cx,cy,r):
    cloud(d,cx,cy-.25*r,r*.95,darken(GREY,.22))
    d.polygon([(cx-4,cy),(cx+13,cy),(cx+1,cy+15),(cx+15,cy+15),(cx-11,cy+.9*r),(cx+1,cy+19),(cx-15,cy+19)],fill=(245,230,120))

def ec_sandstorm(img,d,cx,cy,r):
    d.polygon([(cx-r,cy+.6*r),(cx-.3*r,cy-.1*r),(cx+.2*r,cy+.4*r),(cx+r,cy-.2*r),(cx+r,cy+.6*r)],fill=TAN)
    for yy in (-.4,-.15,.1):
        d.arc([cx-.6*r,cy+yy*r-14,cx+r,cy+yy*r+14],190,60,fill=lighten(TAN,.28),width=4)

def ec_twilight(img,d,cx,cy,r):
    grad_disc(img,cx,cy,r,(70,60,110),(220,150,150))
    R(d,cx-r,cy+.45*r,cx+r,cy+r,fill=(40,35,55))
    d.polygon(starpts(cx-.4*r,cy-.4*r,.18*r,4,.3),fill=(245,240,210))
    E(d,cx+.2*r,cy-.5*r,cx+.55*r,cy-.15*r,fill=SILVER); E(d,cx+.32*r,cy-.55*r,cx+.7*r,cy-.1*r,fill=(150,120,140))

def ec_long_shadows(img,d,cx,cy,r):
    R(d,cx-r,cy+.45*r,cx+r,cy+r,fill=darken(AMBER,.45))
    E(d,cx+.55*r,cy-.55*r,cx+1.05*r,cy-.05*r,fill=AMBER)     # low sun
    d.polygon([(cx-.1*r,cy+.45*r),(cx+.1*r,cy+.45*r),(cx-.7*r,cy+.95*r),(cx-1*r,cy+.95*r)],fill=(35,30,45))
    R(d,cx-.06*r,cy-.2*r,cx+.06*r,cy+.45*r,fill=(35,30,45)) # post
    E(d,cx-.25*r,cy-.5*r,cx+.25*r,cy,fill=darken(GREEN,.2))

def ec_gloom(img,d,cx,cy,r):
    cloud(d,cx,cy-.35*r,r*1.0,darken(GREY,.35))
    for i in range(5):
        x=cx-.6*r+i*.3*r
        d.line([x,cy+.2*r,x,cy+.8*r],fill=darken(GREY,.45),width=5)

def ec_shadow_deepens(img,d,cx,cy,r):
    for k,rr in enumerate((1,.72,.46,.24)):
        E(d,cx-rr*r,cy-rr*r,cx+rr*r,cy+rr*r,fill=darken((90,90,120),.12*k))

def ec_smoke_dolguldur(img,d,cx,cy,r):
    R(d,cx-.28*r,cy-.1*r,cx+.28*r,cy+.9*r,fill=darken(STONE,.45))   # tower
    R(d,cx-.34*r,cy-.25*r,cx+.34*r,cy-.1*r,fill=darken(STONE,.55))
    for dx,sc in [(-.05,1),(.15,.8)]:
        for k in range(4):
            yy=cy-.2*r-k*.22*r; xx=cx+dx*r+(8 if k%2 else -8)
            E(d,xx-.16*r*sc,yy-.16*r*sc,xx+.16*r*sc,yy+.16*r*sc,fill=darken(SMOKE,.1)+(0,) if False else SMOKE)

def ec_pollution(img,d,cx,cy,r):
    R(d,cx-.5*r,cy+.2*r,cx-.05*r,cy+.85*r,fill=darken(STONE,.4))
    R(d,cx+.05*r,cy+.35*r,cx+.45*r,cy+.85*r,fill=darken(STONE,.5))
    for xx,yb in [(cx-.28*r,cy+.2*r),(cx+.25*r,cy+.35*r)]:
        for k in range(3):
            E(d,xx-.18*r,yb-.25*r-k*.2*r,xx+.18*r,yb-.05*r-k*.2*r,fill=lighten(SWAMP,.1))
    for dx in (-.45,-.15,.2):
        d.line([cx+dx*r,cy+.85*r,cx+dx*r,cy+1*r],fill=darken(SWAMP,.1),width=4)

def ec_doors_of_night(img,d,cx,cy,r):
    R(d,cx-.7*r,cy-r,cx+.7*r,cy+r,fill=(15,16,28))
    for px in (-.6,.6): R(d,cx+px*r-8,cy-r,cx+px*r+8,cy+r,fill=STONE)
    R(d,cx-.7*r,cy-r,cx+.7*r,cy-.8*r,fill=STONE)
    for sx,sy in [(-.2,-.4),(.25,-.1),(0,.35),(-.3,.2),(.15,.5)]:
        d.polygon(starpts(cx+sx*r,cy+sy*r,.1*r,4,.3),fill=(220,220,200))

def ec_snow(img,d,cx,cy,r):
    for i in range(6):
        a=i*math.pi/3; ex,ey=cx+r*math.cos(a),cy+r*math.sin(a)
        d.line([cx,cy,ex,ey],fill=ICE,width=4)
        mx,my=cx+.6*r*math.cos(a),cy+.6*r*math.sin(a)
        for s in (-.5,.5): d.line([mx,my,mx+12*math.cos(a+s),my+12*math.sin(a+s)],fill=ICE,width=3)
    E(d,cx-6,cy-6,cx+6,cy+6,fill=WHITE)

def ec_cruel_winter(img,d,cx,cy,r):
    R(d,cx-r,cy-.7*r,cx+r,cy-.5*r,fill=darken(ICE,.25))
    random.seed(3)
    for i in range(6):
        x=cx-.85*r+i*.34*r; h=random.uniform(.5,1.1)*r
        d.polygon([(x-9,cy-.5*r),(x+9,cy-.5*r),(x,cy-.5*r+h)],fill=ICE)
        d.polygon([(x-4,cy-.5*r),(x+4,cy-.5*r),(x,cy-.5*r+h*.6)],fill=WHITE)

def ec_frozen_passes(img,d,cx,cy,r):
    peak(d,cx-.5*r,cy+.8*r,.55*r,1.3*r,darken(ICE,.35))
    peak(d,cx+.5*r,cy+.8*r,.55*r,1.3*r,darken(ICE,.3))
    d.polygon([(cx-.12*r,cy+.85*r),(cx+.12*r,cy+.85*r),(cx+.04*r,cy+.1*r),(cx-.04*r,cy+.1*r)],fill=lighten(BLUE,.3))

def ec_caradhras(img,d,cx,cy,r):
    d.polygon([(cx-.85*r,cy+.85*r),(cx,cy-r),(cx+.5*r,cy+.1*r),(cx+.85*r,cy+.85*r)],fill=darken(RED,.3))
    d.polygon([(cx-.25*r,cy-.1*r),(cx,cy-r),(cx+.18*r,cy-.2*r)],fill=WHITE)
    d.arc([cx-.2*r,cy-1.2*r,cx+.9*r,cy-.4*r],190,40,fill=lighten(SILVER,.2),width=4)

def ec_barrow_chill(img,d,cx,cy,r):
    col=ICE
    E(d,cx-.6*r,cy-.7*r,cx+.6*r,cy+.45*r,fill=col)
    R(d,cx-.35*r,cy+.2*r,cx+.35*r,cy+.7*r,fill=col)
    E(d,cx-.42*r,cy-.35*r,cx-.08*r,cy,fill=(30,40,55)); E(d,cx+.08*r,cy-.35*r,cx+.42*r,cy,fill=(30,40,55))
    d.polygon([(cx,cy-.05*r),(cx-6,cy+.2*r),(cx+6,cy+.2*r)],fill=(30,40,55))
    for dx in (-.3,0,.3): d.line([cx+dx*r,cy+.45*r,cx+dx*r,cy+.7*r],fill=(30,40,55),width=3)

def ec_wildfire(img,d,cx,cy,r):
    R(d,cx-r,cy+.6*r,cx+r,cy+.85*r,fill=darken(BROWN,.4))
    for i,(dx,sc) in enumerate([(-.6,.55),(-.15,.8),(.35,.6),(.7,.45)]):
        flame(d,cx+dx*r,cy+.25*r,r*sc,AMBER if i%2 else RED)

def ec_fires_of_doom(img,d,cx,cy,r):
    d.polygon([(cx-r,cy+.85*r),(cx-.3*r,cy-.3*r),(cx+.3*r,cy-.3*r),(cx+r,cy+.85*r)],fill=darken(STONE,.55))
    d.polygon([(cx-.3*r,cy-.3*r),(cx+.3*r,cy-.3*r),(cx+.15*r,cy),(cx-.15*r,cy)],fill=LAVA)
    flame(d,cx,cy-.45*r,r*.5,LAVA)
    for dx in (-.2,.18): d.line([cx+dx*r,cy-.2*r,cx+dx*r*1.6,cy+.5*r],fill=lighten(LAVA,.1),width=5)

def ec_ashes(img,d,cx,cy,r):
    d.polygon([(cx-r,cy+.85*r),(cx,cy+.1*r),(cx+r,cy+.85*r)],fill=darken((90,70,70),.2))
    E(d,cx-.25*r,cy+.3*r,cx+.25*r,cy+.7*r,fill=LAVA)
    random.seed(5)
    for _ in range(12):
        x=cx+random.uniform(-.9,.9)*r; y=cy+random.uniform(-.9,.4)*r
        E(d,x-3,y-3,x+3,y+3,fill=GREY)

def ec_drown(img,d,cx,cy,r):
    E(d,cx-r,cy-r,cx+r,cy+r,fill=darken(WATER,.25))
    pts=[]
    for i in range(60):
        a=i*0.5; rad=r*(1-i/70)
        pts.append((cx+rad*math.cos(a),cy+rad*math.sin(a)))
    d.line(pts,fill=lighten(WATER,.35),width=4)

def ec_fury_ulmo(img,d,cx,cy,r):
    d.pieslice([cx-r,cy-.6*r,cx+.9*r,cy+1.3*r],180,360,fill=WATER)
    d.arc([cx-.7*r,cy-.4*r,cx+r,cy+1.2*r],180,330,fill=lighten(WATER,.4),width=8)
    for fx in (-.4,0,.4):
        E(d,cx+fx*r-5,cy-.55*r,cx+fx*r+5,cy-.4*r,fill=WHITE)

def ec_rage_ulmo(img,d,cx,cy,r):
    R(d,cx-r,cy+.55*r,cx+r,cy+r,fill=darken(WATER,.2))
    R(d,cx-.05*r,cy-.9*r,cx+.05*r,cy+.6*r,fill=SILVER)           # shaft
    for dx in (-.35,0,.35):
        R(d,cx+dx*r-4,cy-1*r,cx+dx*r+4,cy-.55*r,fill=SILVER)
        d.polygon([(cx+dx*r-7,cy-1*r),(cx+dx*r+7,cy-1*r),(cx+dx*r,cy-1.2*r)],fill=lighten(SILVER,.2))
    R(d,cx-.35*r,cy-.6*r,cx+.35*r,cy-.5*r,fill=SILVER)

def ec_flowers(img,d,cx,cy,r):
    for i in range(6):
        a=i*math.pi/3; px,py=cx+.55*r*math.cos(a),cy+.55*r*math.sin(a)
        E(d,px-.32*r,py-.32*r,px+.32*r,py+.32*r,fill=PINK)
    E(d,cx-.3*r,cy-.3*r,cx+.3*r,cy+.3*r,fill=GOLD)

def ec_butterflies(img,d,cx,cy,r):
    for sgn in (-1,1):
        E(d,cx+sgn*4,cy-.7*r,cx+sgn*r,cy-.02*r,fill=(235,175,90))
        E(d,cx+sgn*6,cy,cx+sgn*.8*r,cy+.6*r,fill=lighten((235,175,90),.25))
    d.line([cx,cy-.6*r,cx,cy+.5*r],fill=(40,30,20),width=4)
    d.line([cx,cy-.6*r,cx-10,cy-.85*r],fill=(40,30,20),width=2); d.line([cx,cy-.6*r,cx+10,cy-.85*r],fill=(40,30,20),width=2)

def ec_willow(img,d,cx,cy,r):
    R(d,cx-.16*r,cy-.1*r,cx+.16*r,cy+.85*r,fill=darken(BROWN,.2))
    E(d,cx-.8*r,cy-.85*r,cx+.8*r,cy+.2*r,fill=darken(GREEN,.1))
    for dx in range(-3,4):
        x=cx+dx*.2*r
        d.line([x,cy-.1*r,x+random.uniform(-4,4),cy+.55*r],fill=GREEN,width=3)
    E(d,cx-.3*r,cy+.1*r,cx-.1*r,cy+.3*r,fill=(20,20,20)); E(d,cx+.1*r,cy+.1*r,cx+.3*r,cy+.3*r,fill=(20,20,20))

def ec_muddy_lane(img,d,cx,cy,r):
    d.polygon([(cx-.25*r,cy-.9*r),(cx+.25*r,cy-.9*r),(cx+r,cy+.9*r),(cx-r,cy+.9*r)],fill=MUD)
    for yy in (-.4,.1,.55):
        E(d,cx-.3*r,cy+yy*r,cx+.1*r,cy+yy*r+.12*r,fill=darken(MUD,.3))
    d.line([cx-.15*r,cy-.7*r,cx-.6*r,cy+.8*r],fill=darken(MUD,.35),width=4)
    d.line([cx+.15*r,cy-.7*r,cx+.6*r,cy+.8*r],fill=darken(MUD,.35),width=4)

def ec_durins_day(img,d,cx,cy,r):
    d.pieslice([cx-.7*r,cy-.9*r,cx+.7*r,cy+.5*r],180,360,fill=darken(STONE,.4))
    R(d,cx-.7*r,cy-.2*r,cx+.7*r,cy+.85*r,fill=darken(STONE,.4))
    d.pieslice([cx-.5*r,cy-.7*r,cx+.5*r,cy+.3*r],180,360,fill=(28,30,40))
    R(d,cx-.5*r,cy,cx+.5*r,cy+.85*r,fill=(28,30,40))
    E(d,cx-.18*r,cy-.55*r,cx+.18*r,cy-.2*r,fill=GOLD)          # moon-rune
    d.line([cx,cy-.2*r,cx,cy+.5*r],fill=GOLD,width=3)

def ec_elves_west(img,d,cx,cy,r):
    R(d,cx-r,cy+.55*r,cx+r,cy+r,fill=darken(WATER,.15))
    d.polygon([(cx-.7*r,cy+.55*r),(cx+.7*r,cy+.55*r),(cx+.45*r,cy+.85*r),(cx-.45*r,cy+.85*r)],fill=WHITE)
    R(d,cx-.02*r,cy-.8*r,cx+.02*r,cy+.55*r,fill=BROWN)
    d.polygon([(cx,cy-.8*r),(cx,cy+.45*r),(cx-.6*r,cy+.35*r)],fill=lighten(SKY,.3))
    d.polygon([(cx,cy-.6*r),(cx,cy+.45*r),(cx+.55*r,cy+.35*r)],fill=WHITE)

def ec_ravens(img,d,cx,cy,r):
    E(d,cx-.3*r,cy-.15*r,cx+.35*r,cy+.6*r,fill=(40,40,55))
    d.polygon([(cx,cy),(cx-r,cy-.5*r),(cx-.3*r,cy+.15*r)],fill=(45,45,60))
    d.polygon([(cx,cy),(cx+r,cy-.45*r),(cx+.3*r,cy+.15*r)],fill=(30,30,45))
    E(d,cx-.18*r,cy-.5*r,cx+.18*r,cy-.15*r,fill=(40,40,55))
    d.polygon([(cx+.1*r,cy-.4*r),(cx+.5*r,cy-.33*r),(cx+.1*r,cy-.25*r)],fill=GOLD)

def ec_restless_east(img,d,cx,cy,r):
    for sgn in (-1,1):
        d.arc([cx-sgn*.2*r-.7*r,cy-.7*r,cx-sgn*.2*r+.7*r,cy+.7*r],
              (300 if sgn>0 else 60),(60 if sgn>0 else 180),fill=STONE,width=8)
        hx=cx+sgn*.55*r
        R(d,hx-4,cy+.3*r,hx+4,cy+.7*r,fill=BROWN)
    E(d,cx-.18*r,cy-.55*r,cx+.18*r,cy-.2*r,fill=RED)            # rising red sun

def ec_unquiet_dead(img,d,cx,cy,r):
    R(d,cx-r,cy+.6*r,cx+r,cy+.9*r,fill=darken(GREEN,.4))       # ground
    d.pieslice([cx-.55*r,cy-.6*r,cx+.55*r,cy+.5*r],180,360,fill=STONE)  # headstone top
    R(d,cx-.55*r,cy-.05*r,cx+.55*r,cy+.65*r,fill=STONE)
    d.line([cx-.25*r,cy+.15*r,cx+.25*r,cy+.15*r],fill=darken(STONE,.35),width=4)
    d.line([cx,cy,cx,cy+.3*r],fill=darken(STONE,.35),width=4)
    # ghost wisp
    E(d,cx+.3*r,cy-.9*r,cx+.8*r,cy-.2*r,fill=GHOST+(0,) if False else GHOST)
    E(d,cx+.42*r,cy-.7*r,cx+.52*r,cy-.6*r,fill=(40,50,60)); E(d,cx+.58*r,cy-.7*r,cx+.68*r,cy-.6*r,fill=(40,50,60))

def ec_wraith(img,d,cx,cy,r):
    d.polygon([(cx,cy-r),(cx+.55*r,cy-.2*r),(cx+.7*r,cy+r),(cx-.7*r,cy+r),(cx-.55*r,cy-.2*r)],fill=(35,30,38))
    d.pieslice([cx-.45*r,cy-r,cx+.45*r,cy],200,340,fill=(20,18,24))
    E(d,cx-.25*r,cy-.45*r,cx-.08*r,cy-.28*r,fill=RED); E(d,cx+.08*r,cy-.45*r,cx+.25*r,cy-.28*r,fill=RED)

# ============================================================================
# TERRAIN
# ============================================================================
def t_mountains(img,d,cx,cy,r):
    grad_disc(img,cx,cy,r,(150,175,205),(110,135,165))
    peak(d,cx-.4*r,cy+.8*r,.6*r,1.1*r,darken(STONE,.2))
    peak(d,cx+.45*r,cy+.8*r,.55*r,1.4*r,STONE)

def t_hills(img,d,cx,cy,r):
    grad_disc(img,cx,cy,r,(150,200,235),(190,225,245))
    for dx,s in [(-.45,.7),(.4,.85),(0,1.0)]:
        E(d,cx+dx*r-s*r,cy+.2*r,cx+dx*r+s*r,cy+1.6*r,fill=darken(GREEN,.05))
    E(d,cx-.2*r,cy+.3*r,cx+.4*r,cy+1*r,fill=GREEN)

def t_plains(img,d,cx,cy,r):
    grad_disc(img,cx,cy,r,(150,200,235),(180,210,140))
    R(d,cx-r,cy+.2*r,cx+r,cy+r,fill=lighten(GREEN,.15))
    for x in range(-4,5):
        xx=cx+x*.22*r; d.line([xx,cy+.45*r,xx,cy+.25*r],fill=darken(GREEN,.1),width=3)

def t_grasslands(img,d,cx,cy,r):
    grad_disc(img,cx,cy,r,(150,200,235),(150,195,110))
    R(d,cx-r,cy+.1*r,cx+r,cy+r,fill=GREEN)
    for x in range(-4,5):
        xx=cx+x*.22*r
        d.line([xx,cy+.6*r,xx-6,cy-.05*r],fill=darken(GREEN,.2),width=3)
        d.line([xx,cy+.6*r,xx+6,cy-.05*r],fill=lighten(GREEN,.1),width=3)

def t_shore(img,d,cx,cy,r):
    grad_disc(img,cx,cy,r,(150,200,235),SAND)
    d.pieslice([cx-r,cy-.2*r,cx+r,cy+2*r],180,360,fill=SAND)
    R(d,cx-r,cy-r,cx+r,cy+.1*r,fill=WATER)
    for yy in (-.5,-.25):
        d.line([cx-.8*r,cy+yy*r,cx+.8*r,cy+yy*r],fill=lighten(WATER,.3),width=3)

def t_forest(img,d,cx,cy,r):
    grad_disc(img,cx,cy,r,(150,200,235),(120,150,110))
    pine(d,cx-.45*r,cy+.85*r,.9*r,darken(GREEN,.1))
    pine(d,cx+.4*r,cy+.85*r,.8*r,darken(GREEN,.05))
    pine(d,cx,cy+.95*r,1.1*r,GREEN)

def t_shallow_water(img,d,cx,cy,r):
    grad_disc(img,cx,cy,r,lighten(WATER,.35),WATER)
    for i,yy in enumerate((-.5,-.15,.2,.55)):
        d.arc([cx-.9*r,cy+yy*r-12,cx+.9*r,cy+yy*r+12],190,350,fill=lighten(WATER,.4),width=4)

def t_deep_water(img,d,cx,cy,r):
    grad_disc(img,cx,cy,r,WATER,darken(DWATER,.25))
    for yy in (-.4,0,.4):
        d.arc([cx-.9*r,cy+yy*r-12,cx+.9*r,cy+yy*r+12],190,350,fill=lighten(DWATER,.3),width=4)

def t_swamp(img,d,cx,cy,r):
    grad_disc(img,cx,cy,r,darken(SWAMP,.1),darken(SWAMP,.3))
    for px in (-.4,.2):
        E(d,cx+px*r-.18*r,cy+.3*r,cx+px*r+.18*r,cy+.45*r,fill=darken(GREEN,.25))
    for x in (-.5,-.2,.4):
        d.line([cx+x*r,cy+.5*r,cx+x*r,cy-.2*r],fill=darken(GREEN,.2),width=4)
    E(d,cx+.25*r,cy+.1*r,cx+.55*r,cy+.3*r,fill=GREEN)          # lily pad

def t_desert(img,d,cx,cy,r):
    grad_disc(img,cx,cy,r,(245,210,150),SAND)
    E(d,cx+.35*r,cy-.45*r,cx+.7*r,cy-.1*r,fill=lighten(GOLD,.1))
    d.pieslice([cx-r,cy+.1*r,cx+.3*r,cy+1.6*r],180,360,fill=darken(SAND,.12))
    d.pieslice([cx-.1*r,cy+.3*r,cx+1.3*r,cy+1.8*r],180,360,fill=darken(SAND,.22))

def t_wastelands(img,d,cx,cy,r):
    grad_disc(img,cx,cy,r,(110,95,95),(70,62,62))
    for (x0,y0,x1,y1) in [(-.6,.6,-.1,-.1),(-.1,-.1,.4,.5),(.4,.5,.8,-.2),(-.1,-.1,.1,.85)]:
        d.line([cx+x0*r,cy+y0*r,cx+x1*r,cy+y1*r],fill=(40,35,35),width=4)

# ============================================================================
# TERRAIN FEATURES
# ============================================================================
def f_road(img,d,cx,cy,r):
    d.polygon([(cx-.25*r,cy-.9*r),(cx+.25*r,cy-.9*r),(cx+r,cy+.9*r),(cx-r,cy+.9*r)],fill=TAN)
    for yy in (-.6,-.1,.4):
        d.line([cx,cy+yy*r,cx,cy+yy*r+.18*r],fill=WHITE,width=4)

def f_river(img,d,cx,cy,r):
    pts=[]
    for i in range(0,41):
        x=cx-r+i*r/20; y=cy+math.sin(i/3)* .35*r
        pts.append((x,y))
    d.line(pts,fill=WATER,width=16); d.line(pts,fill=lighten(WATER,.3),width=6)

def f_pond(img,d,cx,cy,r):
    E(d,cx-.8*r,cy-.3*r,cx+.8*r,cy+.6*r,fill=WATER)
    E(d,cx-.45*r,cy-.05*r,cx+.05*r,cy+.2*r,outline=lighten(WATER,.4),width=3)
    for x in (-.55,-.3): d.line([cx+x*r,cy-.1*r,cx+x*r,cy-.6*r],fill=GREEN,width=4)

def f_bridge(img,d,cx,cy,r):
    R(d,cx-r,cy+.2*r,cx+r,cy+.9*r,fill=WATER)
    d.arc([cx-.7*r,cy-.2*r,cx+.7*r,cy+1.1*r],180,360,fill=darken(BROWN,.1),width=12)
    R(d,cx-r,cy-.4*r,cx+r,cy-.25*r,fill=BROWN)
    for x in (-.7,-.35,0,.35,.7): R(d,cx+x*r-3,cy-.4*r,cx+x*r+3,cy-.15*r,fill=BROWN)

def f_watchtower(img,d,cx,cy,r):
    R(d,cx-.3*r,cy-.5*r,cx+.3*r,cy+.9*r,fill=STONE)
    for x in (-.3,-.1,.1,.3): R(d,cx+x*r-.06*r,cy-.7*r,cx+x*r+.06*r,cy-.5*r,fill=STONE)
    R(d,cx-.1*r,cy-.2*r,cx+.1*r,cy+.1*r,fill=(40,40,55))
    d.polygon([(cx-.4*r,cy-.5*r),(cx+.4*r,cy-.5*r),(cx,cy-.85*r)],fill=darken(RED,.1))

def f_lighthouse(img,d,cx,cy,r):
    d.polygon([(cx-.32*r,cy+.9*r),(cx+.32*r,cy+.9*r),(cx+.2*r,cy-.55*r),(cx-.2*r,cy-.55*r)],fill=WHITE)
    for k in range(3):
        yy=cy+.9*r-k*.45*r
        d.polygon([(cx-.3*r+k*.03*r,yy),(cx+.3*r-k*.03*r,yy),(cx+.28*r-k*.03*r,yy-.12*r),(cx-.28*r+k*.03*r,yy-.12*r)],fill=RED)
    E(d,cx-.22*r,cy-.8*r,cx+.22*r,cy-.5*r,fill=GOLD)
    for a in (-.5,0,.5): d.line([cx,cy-.65*r,cx+r*math.cos(a-1.57),cy-.65*r+r*math.sin(a-1.57)],fill=lighten(GOLD,.3),width=3)

def f_ruins(img,d,cx,cy,r):
    R(d,cx-r,cy+.7*r,cx+r,cy+.9*r,fill=darken(STONE,.3))
    hs=[1.0,0.6,1.3,0.4]
    for i,h in enumerate(hs):
        x=cx-.75*r+i*.5*r
        R(d,x-.12*r,cy+.7*r,x+.12*r,cy+.7*r-h*r,fill=STONE)
        d.line([x-.12*r,cy+.7*r-h*r,x+.12*r,cy+.7*r-h*r+8],fill=darken(STONE,.3),width=3)

def f_standing_stones(img,d,cx,cy,r):
    R(d,cx-.65*r,cy-.5*r,cx-.3*r,cy+.85*r,fill=STONE)
    R(d,cx+.3*r,cy-.5*r,cx+.65*r,cy+.85*r,fill=STONE)
    R(d,cx-.75*r,cy-.75*r,cx+.75*r,cy-.45*r,fill=darken(STONE,.12))

def f_monument(img,d,cx,cy,r):
    R(d,cx-.5*r,cy+.7*r,cx+.5*r,cy+.9*r,fill=darken(STONE,.2))
    d.polygon([(cx-.2*r,cy+.7*r),(cx+.2*r,cy+.7*r),(cx+.1*r,cy-.8*r),(cx-.1*r,cy-.8*r)],fill=STONE)
    d.polygon([(cx-.1*r,cy-.8*r),(cx+.1*r,cy-.8*r),(cx,cy-r)],fill=lighten(STONE,.15))

def f_village(img,d,cx,cy,r):
    R(d,cx-r,cy+.75*r,cx+r,cy+.9*r,fill=darken(GREEN,.3))
    for hx,hy,s in [(-.45,.2,.45),(.4,.35,.4),(0,.55,.5)]:
        bx,by=cx+hx*r,cy+hy*r
        R(d,bx-s*r*.6,by,bx+s*r*.6,by+s*r*.9,fill=BROWN)
        d.polygon([(bx-s*r*.75,by),(bx+s*r*.75,by),(bx,by-s*r*.6)],fill=darken(RED,.15))

def f_fountain(img,d,cx,cy,r):
    d.pieslice([cx-.8*r,cy+.2*r,cx+.8*r,cy+1.1*r],180,360,fill=STONE)
    E(d,cx-.8*r,cy+.1*r,cx+.8*r,cy+.45*r,fill=WATER)
    R(d,cx-.08*r,cy-.3*r,cx+.08*r,cy+.3*r,fill=STONE)
    for a in (-1,-.3,.4): d.line([cx,cy-.3*r,cx+.6*r*math.cos(a-1.57+math.pi),cy-.3*r+.5*r*abs(math.sin(a))],fill=lighten(WATER,.4),width=4)
    for dx in (-.4,0,.4): d.line([cx+dx*r,cy-.25*r,cx+dx*r*1.3,cy+.25*r],fill=lighten(WATER,.4),width=3)

def f_lava(img,d,cx,cy,r):
    E(d,cx-.85*r,cy-.45*r,cx+.85*r,cy+.7*r,fill=darken(LAVA,.45))
    E(d,cx-.7*r,cy-.3*r,cx+.7*r,cy+.55*r,fill=LAVA)
    E(d,cx-.45*r,cy-.1*r,cx+.45*r,cy+.35*r,fill=lighten(LAVA,.3))
    for x,y in [(-.3,.0),(.25,.2),(.0,.35)]:
        E(d,cx+x*r-5,cy+y*r-5,cx+x*r+5,cy+y*r+5,fill=GOLD)

def f_chasm(img,d,cx,cy,r):
    R(d,cx-r,cy-.9*r,cx+r,cy+.9*r,fill=darken(BROWN,.35))
    d.polygon([(cx-.35*r,cy-.9*r),(cx-.05*r,cy-.4*r),(cx-.3*r,cy),(cx,cy+.5*r),(cx-.2*r,cy+.9*r),
               (cx+.25*r,cy+.9*r),(cx+.05*r,cy+.5*r),(cx+.3*r,cy),(cx+.1*r,cy-.4*r),(cx+.4*r,cy-.9*r)],fill=(15,15,22))

def f_mine(img,d,cx,cy,r):
    d.pieslice([cx-.7*r,cy-.4*r,cx+.7*r,cy+1.4*r],180,360,fill=darken(BROWN,.2))
    d.pieslice([cx-.45*r,cy-.1*r,cx+.45*r,cy+1.4*r],180,360,fill=(15,15,22))
    R(d,cx-.7*r,cy+.55*r,cx+.7*r,cy+.9*r,fill=darken(BROWN,.2))
    d.line([cx-.55*r,cy-.55*r,cx+.55*r,cy+.05*r],fill=STONE,width=5)   # pick handle
    d.arc([cx-.7*r,cy-.85*r,cx-.1*r,cy-.25*r],300,120,fill=SILVER,width=5)

def f_blighted(img,d,cx,cy,r):
    R(d,cx-r,cy+.7*r,cx+r,cy+.95*r,fill=darken(PURPLE,.4))
    R(d,cx-.1*r,cy-.4*r,cx+.1*r,cy+.75*r,fill=darken(BROWN,.35))
    for a,ln in [(-1.1,.7),(-.5,.55),(.3,.6),(1.0,.5)]:
        ex,ey=cx+ln*r*math.cos(a-1.57),cy-.3*r+ln*r*math.sin(a-1.57)
        d.line([cx,cy-.3*r,ex,ey],fill=darken(BROWN,.3),width=4)
    E(d,cx-.18*r,cy+.78*r,cx+.18*r,cy+.92*r,fill=lighten(PURPLE,.1))

# ============================================================================
# sheet renderer
# ============================================================================
BG=(24,26,34); TILE_BG=(38,40,52); EDGE=(70,74,92)
TILE_W,TILE_H=190,220; ICON_CY=92; RAD=56

def font(sz):
    for p in (r"C:\Windows\Fonts\segoeui.ttf",r"C:\Windows\Fonts\arial.ttf"):
        if os.path.exists(p): return ImageFont.truetype(p,sz)
    return ImageFont.load_default()
LBL=font(15); TTL=font(34); SUB=font(16)

def wrap(t,f,maxw,d):
    out=[]; cur=""
    for w in t.split():
        s=(cur+" "+w).strip()
        if d.textlength(s,font=f)<=maxw: cur=s
        else: out.append(cur); cur=w
    if cur: out.append(cur)
    return out[:2]

def render(title,subtitle,items,out,cols):
    rows=math.ceil(len(items)/cols); header=70
    W=cols*TILE_W; H=header+rows*TILE_H
    img=Image.new("RGB",(W,H),BG); d=ImageDraw.Draw(img)
    d.text((14,16),title,font=TTL,fill=(235,225,200))
    d.text((W-14-d.textlength(subtitle,font=SUB),30),subtitle,font=SUB,fill=(150,155,170))
    for i,(name,fn,accent) in enumerate(items):
        c,rw=i%cols,i//cols; x0=c*TILE_W; y0=header+rw*TILE_H
        d.rounded_rectangle([x0+6,y0+6,x0+TILE_W-6,y0+TILE_H-6],radius=12,fill=TILE_BG,outline=EDGE,width=2)
        d.rounded_rectangle([x0+6,y0+6,x0+TILE_W-6,y0+14],radius=6,fill=darken(accent,.1))
        fn(img,d,x0+TILE_W//2,y0+ICON_CY,RAD)
        d=ImageDraw.Draw(img)  # refresh after any paste
        for j,ln in enumerate(wrap(name,LBL,TILE_W-24,d)):
            tw=d.textlength(ln,font=LBL)
            d.text((x0+(TILE_W-tw)/2,y0+TILE_H-18-(len(wrap(name,LBL,TILE_W-24,d))-1)*18+j*18),ln,font=LBL,fill=(225,225,230))
    img.save(out); print(f"Saved {out} ({W}x{H}, {len(items)})")

# ----------------------------------------------------------------------------
ENV=[
 ("Sun",ec_sun,GOLD),("Red Sun",ec_red_sun,RED),("Cloudless Day",ec_cloudless,SKY),
 ("Gates of Morning",ec_gates_morning,AMBER),("Dawn",ec_dawn,PINK),
 ("FirstLightOnTheThirdDay",ec_first_light,GOLD),("Light Through Cloud",ec_light_cloud,GOLD),
 ("A Light in Dark Places",ec_a_light,GOLD),
 ("Moon",ec_moon,SILVER),("Full Moon",ec_full_moon,SILVER),("New Moon",ec_new_moon,NIGHT),
 ("Stars",ec_stars,(235,235,200)),("Star of Earendil",ec_earendil,(245,245,210)),
 ("Starlight Dispatch",ec_dispatch,SILVER),("Far Shore, Faded Star",ec_faded_star,(180,185,210)),
 ("Rain",ec_rain,BLUE),("Clouds",ec_clouds,GREY),("Fog",ec_fog,GREY),("Wind",ec_wind,SKY),
 ("Lightning Storm",ec_lightning,(245,230,120)),("Sand Storm",ec_sandstorm,TAN),
 ("Twilight",ec_twilight,PURPLE),("Long Shadows",ec_long_shadows,AMBER),("Gloom",ec_gloom,NIGHT),
 ("The Shadow Deepens",ec_shadow_deepens,(70,70,110)),("Smoke of Dol Guldur",ec_smoke_dolguldur,SMOKE),
 ("Pollution",ec_pollution,SWAMP),("Doors of Night",ec_doors_of_night,NIGHT),
 ("Snow",ec_snow,ICE),("Cruel Winter",ec_cruel_winter,BLUE),("Frozen Passes",ec_frozen_passes,ICE),
 ("Caradhras",ec_caradhras,SILVER),("Barrow Chill",ec_barrow_chill,ICE),
 ("WildFire",ec_wildfire,AMBER),("FiresOfDoom",ec_fires_of_doom,RED),("AshesOfGorgoroth",ec_ashes,(180,80,60)),
 ("Drown",ec_drown,BLUE),("Fury of Ulmo",ec_fury_ulmo,WATER),("RageOfUlmo",ec_rage_ulmo,WATER),
 ("Flowers",ec_flowers,PINK),("Butterflies",ec_butterflies,AMBER),("Old Man Willow Song",ec_willow,GREEN),
 ("Muddy Lane",ec_muddy_lane,BROWN),("Durin's Day",ec_durins_day,GOLD),("ElvesGoingWest",ec_elves_west,SKY),
 ("Ravens",ec_ravens,(90,90,110)),("Restless East",ec_restless_east,(200,120,80)),
 ("Unquiet Dead",ec_unquiet_dead,BONE),
 ("Adunaphel Unleashed",ec_wraith,RED),("Akhorahil Unleashed",ec_wraith,RED),("Dwar Unleashed",ec_wraith,RED),
 ("Hoarmurath Unleashed",ec_wraith,RED),("Ji Indur Unleashed",ec_wraith,RED),("Khamul Unleashed",ec_wraith,RED),
 ("Murazor Unleashed",ec_wraith,RED),("Ovatha Unleashed",ec_wraith,RED),("Ren Unleashed",ec_wraith,RED),
]

TERRAIN=[
 ("Mountains",t_mountains,STONE),("Hills",t_hills,GREEN),("Plains",t_plains,LEAF),
 ("Grasslands",t_grasslands,GREEN),("Shore",t_shore,SAND),("Forest",t_forest,darken(GREEN,.1)),
 ("Shallow Water",t_shallow_water,lighten(WATER,.2)),("Deep Water",t_deep_water,DWATER),
 ("Swamp",t_swamp,SWAMP),("Desert",t_desert,SAND),("Wastelands",t_wastelands,(90,80,80)),
]

FEATURES=[
 ("Road",f_road,TAN),("River",f_river,WATER),("Pond",f_pond,WATER),("Bridge",f_bridge,BROWN),
 ("Watchtower",f_watchtower,STONE),("Lighthouse",f_lighthouse,GOLD),("Ruins",f_ruins,STONE),
 ("Standing Stones",f_standing_stones,STONE),("Monument",f_monument,STONE),("Village",f_village,BROWN),
 ("Fountain",f_fountain,WATER),("Lava",f_lava,LAVA),("Chasm",f_chasm,(80,70,60)),
 ("Mine",f_mine,BROWN),("Blighted",f_blighted,PURPLE),
]

# ----------------------------------------------------------------------------
# transparent multi-sprite atlas: bare sprites only, uniform cells, no chrome
# ----------------------------------------------------------------------------
CELL=150          # square cell size in the atlas
ATLAS_COLS=10

def build_atlas(png_path,json_path):
    groups=[("environmental",ENV),("terrain",TERRAIN),("feature",FEATURES)]
    flat=[(cat,name,fn) for cat,lst in groups for (name,fn,_) in lst]
    rows=math.ceil(len(flat)/ATLAS_COLS)
    W,H=ATLAS_COLS*CELL,rows*CELL
    atlas=Image.new("RGBA",(W,H),(0,0,0,0))
    frames=[]
    for i,(cat,name,fn) in enumerate(flat):
        col,row=i%ATLAS_COLS,i//ATLAS_COLS
        tile=Image.new("RGBA",(CELL,CELL),(0,0,0,0))
        d=ImageDraw.Draw(tile,"RGBA")
        fn(tile,d,CELL//2,CELL//2,RAD)
        atlas.alpha_composite(tile,(col*CELL,row*CELL))
        frames.append({"index":i,"name":name,"category":cat,
                       "x":col*CELL,"y":row*CELL,"w":CELL,"h":CELL})
    atlas.save(png_path)
    with open(json_path,"w",encoding="utf-8") as fh:
        json.dump({"image":os.path.basename(png_path),"cell":CELL,
                   "cols":ATLAS_COLS,"rows":rows,"count":len(flat),
                   "frames":frames},fh,indent=2)
    print(f"Saved {png_path} ({W}x{H}, {len(flat)} sprites, transparent)")
    print(f"Saved {json_path}")

def main():
    root=os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    build_atlas(os.path.join(root,"retrolotr_sprites_atlas.png"),
                os.path.join(root,"retrolotr_sprites_atlas.json"))

if __name__=="__main__":
    main()
