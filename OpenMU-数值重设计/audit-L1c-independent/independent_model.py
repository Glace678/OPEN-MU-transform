# -*- coding: utf-8 -*-
import json, math, os, csv

BASE = r"D:\openmu自用\OpenMU-数值重设计\artifacts\_afterchange5"
cat = json.load(open(os.path.join(BASE, "engine-catalog.json"), encoding="utf-8"))
probe = json.load(open(os.path.join(BASE, "combat-probe.json"), encoding="utf-8"))
mon = {m["id"]: m for m in cat["monsters"]}
prof = {p["className"]: p for p in probe["Profile"]}

VILLAGES = {
    "Lorencia(075)": {"Bull Fighter":0,"Hound":1,"Budge Dragon":2,"Spider":3,"Elite Bull":4,"Lich":6,"Giant":7,"Skeleton":14},
    "Noria(075)": {"Goblin":26,"Chain Scorpion":27,"Beetle Monster":28,"Hunter":29,"Forest Monster":30,"Agon":31,"Stone Golem":32,"Elite Goblin":33},
    "Elvenland(S6)": {"Strange Rabbit":418,"Polluted Butterfly":419,"Hideous Rabbit":420,"Werewolf":421,"Cursed Lich":422,"Totem Golem":423,"Grizzly":424,"Captain Grizzly":425},
}
HIT_BASE,HIT_SLOPE,HIT_MIN,HIT_MAX = 0.92,0.08,0.70,0.98
ARMOR_CONST,ARMOR_PER_RANK,RED_CAP = 70.0,2.4,0.60
PLAYER_RANK = 1.0

def hit_chance(acc,eva):
    a,e=max(1,acc),max(1,eva)
    return min(HIT_MAX,max(HIT_MIN,HIT_BASE+HIT_SLOPE*math.log2(a/e)))
def reduction(armor,rank=PLAYER_RANK):
    ar=max(0,armor); return min(RED_CAP, ar/(ar+ARMOR_CONST+ARMOR_PER_RANK*rank))
def floor_dmg(level): return max(1, round(3.5*math.sqrt(level)))

def encounter(cls,mid,kind):
    p=prof[cls]; m=mon[mid]
    bmin = p["minPhys"] if kind=="phys" else p["minWiz"]
    bmax = p["maxPhys"] if kind=="phys" else p["maxWiz"]
    acc=p["attackRatePvm"]; eva=m["evasion"]
    hc=hit_chance(acc,eva); red=reduction(m["armor"]); mult=1-red
    emin=bmin*mult; emax=bmax*mult; avg=(emin+emax)/2
    if avg<=0: return None
    hits=math.ceil(m["hp"]/avg); swings=hits/hc
    return {"cls":cls,"mon":m["name"],"mon_id":mid,"mon_level":m["level"],"hp":m["hp"],"armor":m["armor"],"eva":eva,"acc":acc,"kind":kind,"bmin":round(bmin,2),"bmax":round(bmax,2),"hit":round(hc,4),"red":round(red,4),"mult":round(mult,4),"emin":round(emin,2),"emax":round(emax,2),"avg":round(avg,2),"hits":hits,"swings":round(swings,2),"ttk1":round(swings*1.0,2),"ttk045":round(swings*0.45,2)}

rows=[]
for v,ms in VILLAGES.items():
    for nm,mid in ms.items():
        if mid not in mon:
            rows.append({"village":v,"MISSING":True,"mon":nm,"mon_id":mid}); continue
        for cls in ["Dark Knight","Dark Wizard","Fairy Elf"]:
            if cls=="Dark Wizard":
                rows.append({**encounter(cls,mid,"phys"),"village":v})
                rows.append({**encounter(cls,mid,"wiz"),"village":v})
            else:
                rows.append({**encounter(cls,mid,"phys"),"village":v})

out_csv=os.path.join(os.path.dirname(__file__),"independent_ttk.csv")
keys=["village","cls","mon","mon_id","mon_level","hp","armor","eva","acc","kind","bmin","bmax","hit","red","mult","emin","emax","avg","hits","swings","ttk1","ttk045"]
with open(out_csv,"w",newline="",encoding="utf-8-sig") as f:
    w=csv.DictWriter(f,fieldnames=keys); w.writeheader()
    for r in rows:
        if r.get("MISSING"):
            w.writerow({"village":r["village"],"mon":r["mon"],"mon_id":r["mon_id"],"kind":"MISSING"})
        else:
            w.writerow({k:r.get(k,"") for k in keys})

pe={(e["ClassName"],e["MonsterName"]):e for e in probe["Encounters"]}
cmp=[]
for r in rows:
    if r.get("MISSING") or r["kind"]!="phys": continue
    if (r["cls"],r["mon"]) in pe:
        e=pe[(r["cls"],r["mon"])]
        cmp.append({"cls":r["cls"],"mon":r["mon"],"my_hit":r["hit"],"probe_hit":round(e["BalanceV1HitChance"],4),"my_mult":r["mult"],"probe_mult":round(e["damageMultiplier"],4),"my_hits":r["hits"],"probe_hits":e["ExpectedHits"],"my_ttk":r["ttk1"],"probe_ttk":round(e["TtkSeconds"],2)})
out_cmp=os.path.join(os.path.dirname(__file__),"crosscheck_vs_probe.csv")
with open(out_cmp,"w",newline="",encoding="utf-8-sig") as f:
    w=csv.DictWriter(f,fieldnames=list(cmp[0].keys())); w.writeheader(); w.writerows(cmp)

print("=== profile(runtime) ===")
for c,p in prof.items():
    print(f"  {c:11s} Str{p['totalStr']} Agi{p['totalAgi']} Ene{p['totalEnergy']} phys {p['minPhys']}-{p['maxPhys']} wiz {p['minWiz']}-{p['maxWiz']} atkRate {p['attackRatePvm']}")
print("\n=== village monster catalog ===")
for v,ms in VILLAGES.items():
    print(f"[{v}]")
    for nm,mid in ms.items():
        if mid in mon:
            m=mon[mid]; print(f"  #{mid:>3} {nm:20s} lvl{m['level']:>3} HP{m['hp']:>4} armor{m['armor']:>3} acc{m['accuracy']:>4} eva{m['evasion']:>4} dmg{m['minDamage']}-{m['maxDamage']} atkDelay{m['attackSeconds']}s")
        else:
            print(f"  #{mid:>3} {nm:20s} !!! MISSING in catalog")
print("\n=== lowest mob per village ===")
for vname,nm,mid in [("Lorencia","Spider",3),("Noria","Goblin",26),("Elvenland","StrangeRabbit",418)]:
    for cls in ["Dark Knight","Dark Wizard","Fairy Elf"]:
        kinds=["phys","wiz"] if cls=="Dark Wizard" else ["phys"]
        for k in kinds:
            r=encounter(cls,mid,k); tag="WIZ魔法" if k=="wiz" else ("DW普攻=物理" if cls=="Dark Wizard" else "物理")
            print(f"  {vname:12s} {nm:13s} vs {cls:11s}[{tag:11s}] hit{r['hit']*100:5.1f}% eff{r['emin']:.1f}-{r['emax']:.1f} hits{r['hits']} TTK(1s){r['ttk1']:5.1f}s (0.45s){r['ttk045']:4.1f}s")
print("\n=== lockstep vs probe (phys DK/FE) ===")
allm=True
for c in cmp:
    ok=abs(c["my_hit"]-c["probe_hit"])<0.002 and abs(c["my_mult"]-c["probe_mult"])<0.002 and abs(c["my_ttk"]-c["probe_ttk"])<0.1
    allm=allm and ok
    print(f"  {'OK ' if ok else 'DIFF'} {c['cls']:11s} {c['mon']:14s} hit {c['my_hit']}/{c['probe_hit']} mult {c['my_mult']}/{c['probe_mult']} hits {c['my_hits']}/{c['probe_hits']} ttk {c['my_ttk']}/{c['probe_ttk']}")
print(f"\nPHYS model lockstep match probe: {allm}")
print("CSV:",out_csv); print("CMP:",out_cmp)