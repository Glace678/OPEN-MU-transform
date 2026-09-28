# -*- coding: utf-8 -*-
import json, os, re, sys
sys.path.insert(0,W)
from gap_engine import ID_SLOT,ID_PHRASE,ID_WORD, W, STR, WL
from gap_add import ID_ADD, ID_PHRASE_ADD

# ---------- TL (Tagalog/Filipino; English game loanwords allowed) ----------
TL_SLOT={
 'armor':'Armadura','boots':'Bota','gloves':'Guwantes','helm':'Helmet','helmet':'Helmet',
 'pants':'Pantalon','mask':'Maskara','shield':'Kalasag','stick':'Sulo','staff':'Staff',
 'sword':'Espada','bow':'Pana','crossbow':'Crossbow','axe':'Palakol','blade':'Talim',
 'scepter':'Scepter','spear':'Sibat','mace':'Mace','scythe':'Karit','hammer':'Martilyo',
 'trident':'Trident','lance':'Lansa','dagger':'Daga',
 'parchment':'Perkamin','scroll':'Scroll','ticket':'Tiket','potion':'Potion','ring':'Singsing',
 'pendant':'Pendant','necklace':'Kuwintas','box':'Kahon','orb':'Orb','feather':'Balahibo',
 'flame':'Apoy','cape':'Kapa','wing':'Pakpak','wings':'Pakpak','horn':'Sungay','key':'Susi',
 'eye':'Mata','talisman':'Talisman','seal':'Selyo','elixir':'Elixir','charm':'Charm',
 'coin':'Barya','cloak':'Kapa','fragment':'Pira-piraso','stone':'Bato','branch':'Sanga',
 'figurine':'Figurine','card':'Card','pass':'Pass','bundle':'Bundle','package':'Package',
 'book':'Libro','map':'Mapa','gift':'Regalo','fruit':'Prutas','tome':'Tome','glove':'Guwantes',
 'backpack':'Bag','certificate':'Sertipiko','service':'Serbisyo','access':'Access',
 'flower':'Bulaklak','petal':'Talulot','cake':'Cake','wine':'Alak','water':'Tubig'}
TL_WORD={
 'dragon':'Dragon','dark':'Madilim','soul':'Kaluluwa','bone':'Buto','brass':'Brass','bronze':'Bronze',
 'ancient':'Sinauna','black':'Itim','phoenix':'Phoenix','steel':'Bakal','master':'Master',
 'demonic':'Demong','divine':'Banal','legendary':'Legendary','leather':'Leather','plate':'Plate',
 'eternal':'WalangHanggan','great':'Great','grand':'Grand','light':'Liwanag','lightning':'Kidlat',
 'lighting':'Kidlat','fire':'Apoy','ice':'Yelo','wind':'Hangin','storm':'Bagyo','spirit':'Espiritu',
 'sacred':'Sagrado','silver':'Pilak','golden':'Ginto','gold':'Ginto','red':'Pula','blue':'Asul',
 'green':'Berde','purple':'Lila','pink':'Rosas','white':'Puti','chaos':'Chaos','life':'Buhay',
 'death':'Kamatayan','blood':'Dugo','frost':'Yelo','phantom':'Multo','knight':'Kabalyero',
 'lord':'Panginoon','emperor':'Emperador','fighter':'Fighter','angel':'Anghel','battle':'Labanan',
 'guardian':'Tagapangalaga','healing':'Pagpapagaling','heal':'Galing','mana':'Mana',
 'increase':'Dagdag','increases':'Dagdag','maximum':'Max','minimum':'Min','strength':'Lakas',
 'agility':'Liksi','energy':'Energy','defense':'Depensa','defence':'Depensa','attack':'Atake',
 'damage':'Damage','speed':'Bilis','rate':'Rate','health':'Kalusugan','resistance':'Resist',
 'mastery':'Mastery','strengthener':'Enhance','proficiency':'Proficiency','skill':'Skill',
 'poison':'Lason','sleep':'Tulog','stun':'Stun','blind':'Bulag','freeze':'Freeze','ice':'Yelo',
 'flame':'Apoy','slash':'Slash','summon':'Tawag','arrow':'Pana','shot':'Shot','triple':'Triple',
 'recovery':'Recovery','recover':'Recover','restores':'Restore','fully':'Buo','all':'Lahat',
 'of':'','the':'','s':'','a':'','and':'At','to':'Sa','or':'O','from':'Mula','monster':'Monster',
 'kills':'Kill','automatic':'Automatic','status':'Status','critical':'Critical','double':'Double',
 'ignore':'Ignore','immunity':'Immunity','reduction':'Bawas','decrease':'Baba','boost':'Boost',
 'absorption':'Absorb','wing':'Pakpak','wings':'Pakpak','cape':'Kapa','cloak':'Kapa',
 'party':'Party','exp':'EXP','hp':'HP','mp':'MP','sd':'SD','ag':'AG','pvp':'PvP','zen':'Zen',
 'rank':'Rank','level':'Level','seal':'Selyo','scroll':'Scroll','elixir':'Elixir','talisman':'Talisman',
 'santa':'Santa','christmas':'Pasko','cherry':'Cherry','blossom':'Blossom','ascension':'Ascension',
 'wealth':'Yaman','strength':'Lakas','mobility':'Mobility','sustenance':'Sustenance','divinity':'Divinity',
 'healing':'Pagpapagaling','battle':'Labanan','defense':'Depensa','quickness':'Quickness','wrath':'Wrath',
 'invincibility':'Invincibility','invisibility':'Invisibility','iron':'Bakal','soul':'Kaluluwa',
 'barrier':'Barrier','spell':'Spell','protection':'Proteksyon','restriction':'Restriction',
 'pursuit':'Pursuit','sublimation':'Sublimation','vampiric':'Vampiric','weakness':'Kahinaan',
 'weaken':'Hina','bleeding':'Dugo','cold':'Malamig','fire':'Apoy','watch':'Bantay','watchtower':'Tore',
 'penalty':'Penalty','besiegement':'Siege','contract':'Contract','altar':'Altar','wolf':'Wolf',
 'hero':'Bayani','guild':'Guild','enabled':'Enabled','disabled':'Disabled','attempt':'Attempt',
 'crown':'Crown','registration':'Registration','switch':'Switch','transparency':'Transparency',
 'divine':'Banal','cry':'CryWolf','gate':'Gate','open':'Open','close':'Close','illicit':'Illicit',
 'software':'Software','punishment':'Parusa','use':'Use','magic':'Magic','general':'General',
 'goods':'Goods','merchant':'Merchant','shop':'Shop','owner':'Owner','potion':'Potion','weapons':'Weapons',
 'wandering':'Wandering','stone':'Bato','refining':'Refining','higher':'Higher','lower':'Lower',
 'watchers':'Watchers','kundun':'Kundun','gens':'Gens','ranking':'Ranking','reward':'Reward',
 'mercenary':'Mercenary','office':'Office','free':'Free','letter':'Letter','request':'Request',
 'dialog':'Dialog','script':'Script','error':'Error','inland':'Inland','supply':'Supply','route':'Route',
 'dungeon':'Dungeon','sweep':'Sweep','adaptation':'Adaptation','challenge':'Challenge',
 'whispers':'Whispers','trade':'Trade','hmm':'Hmm','heroic':'Heroic','courage':'Courage',
 'fighting':'Fighting','mentality':'Mentality','qualities':'Qualities','tenacity':'Tenacity',
 'hound':'Hound','hunt':'Hunt','walls':'Walls','beyond':'Beyond','abilities':'Abilities',
 'sure':'Sure','within':'Within','im':'Ako','scorpions':'Scorpion','chain':'Chain','noria':'Noria',
 'lorencia':'Lorencia','tower':'Tower','lost':'Lost','swamp':'Swamp','peace':'Peace','calmness':'Calm',
 'relics':'Relics','ruins':'Ruins','ruin':'Ruin','island':'Island','kanturu':'Kanturu',
 'adds':'Add','command':'Command','stat':'Stat','agility':'Liksi','control':'Control','energy':'Energy',
 'swell':'Swell','life':'Buhay','berserker':'Berserker','bless':'Bless','blessing':'Blessing',
 'decay':'Decay','inferno':'Inferno','hellfire':'Hellfire','cyclone':'Cyclone','twister':'Twister',
 'twisting':'Twisting','spiral':'Spiral','falling':'Falling','lunge':'Lunge','uppercut':'Uppercut',
 'charge':'Charge','cometfall':'Cometfall','meteor':'Meteor','starfall':'Starfall','teleport':'Teleport',
 'ally':'Ally','assassin':'Assassin','bali':'Bali','golem':'Golem','satyros':'Satyros','soldier':'Soldier',
 'vengeance':'Vengeance','cure':'Cure','multi-shot':'MultiShot','phoenix':'Phoenix','plasma':'Plasma',
 'force':'Force','wave':'Wave','power':'Power','rageful':'Rageful','blow':'Blow','killing':'Killing',
 'impale':'Impale','stab':'Stab','earthshake':'Earthshake','earth':'Earth','prison':'Prison',
 'electric':'Electric','spark':'Spark','spike':'Spike','ball':'Ball','evil':'Evil','side':'Side',
 'horse':'Horse','raven':'Raven','howling':'Howling','howl':'Howl','scream':'Scream','breath':'Breath',
 'burst':'Burst','blast':'Blast','tome':'Tome','cast':'Cast','other':'Other','world':'World',
 'mace':'Mace','spear':'Sibat','scepter':'Scepter','shield':'Kalasag','sword':'Espada','staff':'Staff',
 'stick':'Sulo','bow':'Pana','crossbow':'Crossbow','axe':'Palakol','pet':'Pet','slasher':'Slasher',
 'roar':'Roar','drive':'Drive','diseier':'Diseier','chaotic':'Chaotic','combo':'Combo','cancel':'Cancel',
 'invincible':'Invincible','innovate':'Innovate','innovation':'Innovation','penetrate':'Penetrate',
 'penetration':'Penetration','shied-burn':'ShiedBurn','burn':'Burn','shock':'Shock','strike':'Strike',
 'gigantic':'Gigantic','expansion':'Expansion','wizardry':'Wizardry','infinity':'Infinity',
 'abolish':'Abolish','block':'Block','switch':'Switch','set':'Set','equipped':'Equipped',
 'one-handed':'OneHand','two-handed':'TwoHand','durability':'Durability','increment':'Increment',
 'chemistry':'Chemistry','bonus':'Bonus','gladiator':'Gladiator','honor':'Honor','divinity':'Divinity',
 'neil':'Neil','shamut':'Shamut','summoner':'Summoner','rod':'Rod','temple':'Temple','quickness':'Quick',
 'chaos':'Chaos','castle':'Castle','wolf':'Wolf','altar':'Altar','magic':'Magic','marce':'Marce',
 'silvia':'Silvia','herald':'Herald','rhea':'Rhea','leina':'Leina','silver':'Pilak',
}
# ---------- PL (Polish; only its ~161 gap: skills/buffs/setoption/handful) ----------
PL_WORD={
 'increase':'Wzrost','increases':'Wzrost','maximum':'Maks.','minimum':'Min.','strength':'Siła',
 'agility':'Zręczność','energy':'Energia','defense':'Obrona','defence':'Obrona','attack':'Atak',
 'damage':'Obrażenia','speed':'Szybkość','rate':'Szansa','health':'Zdrowie','life':'Życie',
 'resistance':'Odporność','mastery':'Mistrzostwo','strengthener':'Wzmocnienie','proficiency':'Biegłość',
 'skill':'Umiejętność','poison':'Trucizna','sleep':'Sen','stun':'Ogłuszenie','blind':'Oślepienie',
 'freeze':'Zamrożenie','ice':'Lodowy','flame':'Płomień','slash':'Cięcie','summon':'Przywołanie',
 'arrow':'Strzała','shot':'Strzał','triple':'Potrójny','recovery':'Regeneracja','recover':'Regeneruj',
 'restores':'Przywraca','fully':'Całkowicie','all':'całe','automatic':'Automatyczna','status':'Status',
 'critical':'Kryt.','double':'Podwójne','ignore':'Ignoruj','immunity':'Niewrażliwość','reduction':'Redukcja',
 'decrease':'Spadek','boost':'Wzmocnienie','absorption':'Absorpcja','spell':'Zaklęcie','protection':'Ochrona',
 'restriction':'Ograniczenie','pursuit':'Pogoń','soul':'Duszy','barrier':'Bariera','iron':'Żelazna',
 'invisibility':'Niewidzialność','invincibility':'Nietykalność','weakness':'Słabość','weaken':'Osłabienie',
 'bleeding':'Krwawienie','cold':'Ziębnięcie','fire':'Ognia','vampiric':'Wampiryzm','berserker':'Berserk',
 'bless':'Błogosławieństwo','blessing':'Błogosławieństwo','decay':'Rozkład','inferno':'Piekło',
 'hellfire':'PiekielnyOgień','cyclone':'Cyklon','twister':'Tornado','twisting':'Wirujące','spiral':'Spiralne',
 'falling':'Opadające','lunge':'Pchnięcie','uppercut':'GórnyCios','charge':'Szarża','cometfall':'UpadekKomety',
 'meteor':'Meteor','starfall':'Gwiazdopad','teleport':'Teleportacja','ally':'Sojusznika','assassin':'Skrytobójcę',
 'golem':'Golem','soldier':'Żołnierza','vengeance':'Zemsta','cure':'Leczenie','multi-shot':'Wielostrzał',
 'plasma':'Plazma','force':'Siła','wave':'Fala','power':'Moc','rageful':'Gniewny','blow':'Cios',
 'killing':'Zabójczy','impale':'Przebicie','stab':'Pchnięcie','earthshake':'TrzęsienieZiemi','earth':'Ziemi',
 'prison':'Więzienie','electric':'Elektryczny','spark':'Iskra','spike':'Kolec','ball':'Kula','evil':'Zła',
 'dark':'Ciemny','side':'Strona','horse':'Konia','howling':'Wycie','howl':'Wyj','scream':'Krzyk',
 'breath':'Oddech','burst':'Wybuch','blast':'Podmuch','cast':'Rzucanie','other':'Innego','world':'Świata',
 'mace':'Buława','spear':'Włócznia','scepter':'Berło','shield':'Tarcza','sword':'Miecz','staff':'Laska',
 'bow':'Łuk','crossbow':'Kusza','axe':'Topór','roar':'Ryk','chain':'Łańcuch','drive':'Napęd',
 'chaotic':'Chaotyczny','combo':'Combo','cancel':'Anuluj','innovate':'Innowacja','innovation':'Innowacja',
 'penetrate':'Przebij','penetration':'Penetracja','burn':'Spalenie','shock':'Porażenie','strike':'Uderzenie',
 'gigantic':'Olbrzymi','expansion':'Rozszerzenie','wizardry':'Magia','infinity':'Nieskończoność',
 'abolish':'Znieś','magic':'Magii','control':'Kontrola','block':'Blok','equipped':'Wyposażonej',
 'one-handed':'Jednoręcznej','two-handed':'Dwuręcznej','durability':'Wytrzymałość','increment':'Przyrost',
 'bonus':'Bonus','gladiator':'Gladiatora','honor':'Honor','swell':'Wzrost','heal':'Leczenie',
 'healing':'Leczenie','party':'Drużynowe','tome':'Księga','lightning':'Błyskawica','frost':'Mróz',
 'requiem':'Requiem','shied-burn':'SpalTarczy','dark':'Ciemny','spirit':'Duch','flame':'Płomień',
 'strike':'Uderzenie','nova':'Nova','force':'Siła','inferno':'Piekło','cure':'Leczenie',
 'attack':'Atak','success':'Powodzenia','def':'Obr','defense':'Obrona','stamina':'Kondycja',
 'command':'Dowodzenie','monster':'Potwora','kills':'Zabójstw','from':'z','sd':'SD','ag':'AG','hp':'HP',
 'mana':'Mana','pvp':'PvP','exp':'EXP','rate':'Szansa','speed':'Szybkość','recovers':'Przywraca',
 'wing':'Skrzydła','wings':'Skrzydła','cape':'Płaszcz','cloak':'Płaszcz','dimension':'Wymiaru',
 'reigning':'Panującego','emperor':'Cesarza','eternal':'Wieczne','illusion':'Iluzja','ruin':'Zguby',
 'storm':'Burzy','of':'','the':'','s':'','a':'','and':'I','to':'do','or':'lub',
 'adds':'Dodaje','stat':'Statystykę','swell':'Wzrost','life':'Życia','defensibility':'Obronność',
 'regen':'Regen','gain':'Zdobycie','drop':'Drop','item':'Przedmiot','powup':'Wzmoc','pow':'Wzmoc',
 'invincible':'Nietykalny','use':'Użycie','pet':'Zwierzaka','two':'Dwuręcznej','one':'Jednoręcznej',
 'handed':'ręcznej','weapon':'Broń','set':'Zestawu','physical':'Fizyczny','tome':'Księga',
 'wind':'Wiatru','fire':'Ognia','ice':'Lodu','other':'Innego','world':'Świata','scepter':'Berło',
 'stamina':'Kondycja','strengthener':'Wzmocnienie','maximum':'Maks.','increase':'Wzrost',
}
PL_SLOT={} # polish gap has only 4 items, handled by word dict / keep
JA_SENT={1262:'ロレンシアの城壁の外へ出て、ハウンドを30体狩ってください。この狩りはあなたの実力なら十分こなせるはずです。',
         1263:'ノリアの城壁の外へ出て、チェーンスコーピオンを3体狩ってください。'}
TL_PHRASE={
 'attack success rate increase':'Dagdag SuccessRate Atake','increase attack success rate':'Dagdag SuccessRate Atake',
 'death beam knight flame':'Death-beam Knight Apoy','first secromicon fragment':'Secromicon Frag 1',
 'fourth secromicon fragment':'Secromicon Frag 4','fifth secromicon fragment':'Secromicon Frag 5',
 'second secromicon fragment':'Secromicon Frag 2','third secromicon fragment':'Secromicon Frag 3',
 'order guardian life stone':'Order (Guardian/Life Bato)','skeleton transformation ring':'Singsing Skeleton Transform',
 'defsuccessrate increase mastery':'Kasanayan SuccessRate Def','attack success rate increase mastery':'Kasanayan SuccessRate Atk',
}

def norm(s):
    return s.replace('¡¯',"'").replace('’',"'").replace('‘',"'")

PROT=re.compile(r'(%[0-9.]*[dsfuxX%]|\{[0-9]+\}|[0-9]+(?:\.[0-9]+)?|[()/,:+\-]|’s|\x27s)')
def split_tokens(s):
    # returns list of (isword, text); printf placeholders kept atomic & verbatim
    out=[]
    for m in re.finditer(r"%[0-9.]*[dsfuxX%]|\{[0-9]+\}|[A-Za-z]+|[^A-Za-z%]+|%",s):
        g=m.group(0)
        isw=bool(re.match(r'[A-Za-z]+$',g))
        out.append((isw,g))
    return out

def translate_words(s, phrase, word, slot, headfirst=False):
    s=norm(s)
    # protect trailing (n), -J, digits by working on word tokens; keep nonwords as-is
    toks=split_tokens(s)
    words=[(i,t) for i,(w,t) in enumerate(toks) if w]
    lower=[t.lower() for _,t in words]
    tx={}
    # phrase longest-match over the word index
    used=set(); j=0
    while j<len(words):
        wi=words[j][0]; matched=None
        for L in range(4,0,-1):
            if j+L<=len(words):
                key=' '.join(lower[j:j+L])
                if key in phrase:
                    matched=(L,phrase[key]); break
        if matched:
            L,val=matched
            for k in range(j,j+L): tx[words[k][0]]= '' if k>j else val
            for k in range(j,j+L): used.add(k)
            j+=L
        else:
            w=lower[j]; wi0=words[j][0]
            tx[wi0]=word.get(w,None)  # None => keep original
            j+=1
    # head-first reorder for items: locate trailing slot word
    out=[]
    for i,(isw,t) in enumerate(toks):
        if not isw: out.append(t); continue
        v=tx.get(i,'')
        if v is None: out.append(t)  # keep original proper noun
        elif v!='': out.append(v)
    res=''.join(out)
    res=re.sub(r'\s+',' ',res).strip()
    res=res.replace(' ,',',').replace(' .','.').replace('( ','(').replace(' )',')')
    return res

OVERRIDE={
 ('tl',2335):'Order (Guardian/Life Bato)',
 # tl movereq internal tokens (mirror German transliteration, keep digits/underscore)
 ('tl',1955):'Kanturu_Ruina1',('tl',1956):'Kanturu_Ruina2',('tl',1957):'Kanturu_Ruina_Isla',
 # id movereq / standalone
 ('id',929):'BawahTanah',('id',938):'BawahTanah2',('id',939):'BawahTanah3',('id',2236):'Monster 7',
 ('id',1503):'Helm Hades',('id',1873):'Helm Iris',('id',3208):'Pingsan',('id',161):'Bir',
 ('id',159):'Aileen Busur',('id',206):'Arkabus',('id',332):'Bardysh',('id',1117):'Falsion',
 ('id',1170):'RantaiGada',('id',1179):'Flamberge',('id',1506):'TombakKapak',
 # pl movereq / standalone
 ('pl',929):'Loch',('pl',938):'Loch2',('pl',939):'Loch3',('pl',809):'Bies',
 ('pl',2097):'ZagubionaWieża',('pl',2098):'ZagubionaWieża2',('pl',2099):'ZagubionaWieża3',
 ('pl',2100):'ZagubionaWieża4',('pl',2101):'ZagubionaWieża5',('pl',2102):'ZagubionaWieża6',('pl',2103):'ZagubionaWieża7',
 ('pl',1950):'RelikwieKanturu',('pl',1951):'RuinyKanturu1',('pl',1952):'RuinyKanturu2',('pl',1953):'RuinyKanturu3',
 ('pl',2374):'BagnoSpokoju',('pl',2449):'Zanieczyszczenie',
}
# ---- supplement word dicts (translate words previously left as English) ----
ID_WORD2={
 'arquebus':'Arkabus','berdysh':'Bardysh','falchion':'Falsion','flail':'RantaiGada','flameberge':'Flamberge',
 'halberd':'TombakKapak','dungeon':'BawahTanah','stun':'Pingsan','ale':'Bir','bowr':'Busur',
}
TL_WORD2={
 'whispers':'Bulong','trade':'Kalakal','abolish':'Alisin','magic':'Mahika','crossbow':'Tirador',
 'berserker':'Berserker','mastery':'Kasanayan','proficiency':'Kahusayan','cancel':'Kanselahin',
 'invisibility':'Pagkakatago','stun':'Tuliro','cast':'Ipataw','invincibility':'DiMatatalo','chain':'Kadena',
 'drive':'Hatak','card':'Karta','castle':'Kastilyo','charge':'Sugod','cometfall':'Bulalakaw','cure':'Lunas',
 'cyclone':'Bagyo','decay':'Pagkabulok','dragon':'Drakon','roar':'Atungal','slasher':'Mamumutol',
 'dungeon':'Piitan','prison':'Kulungan','earthshake':'Pagyanig','electric':'Kuryente','spike':'Tinik',
 'energy':'Enerhiya','ball':'Bola','falchion':'Kris','falling':'Nahuhulog','slash':'Hiwa','force':'Puwersa',
 'wave':'Alon','gate':'Tarangkahan','open':'Buksan','close':'Isara','status':'Estado','goat':'Kambing',
 'figurine':'Estatwa','staff':'Baston','grand':'Marangal','viper':'Ahas','great':'Malaki','scepter':'Setro',
 'halberd':'TabakPole','hellfire':'ApoyImpyerno','impale':'Tusok','innovate':'Makabago','invincible':'DiMatatalo',
 'lantern':'Parol','wrath':'Galit','killing':'Pamamatay','blow':'Tama','legendary':'Maalamat','mace':'Maso',
 'meteor':'Bulalakaw','potion':'Gamot','charm':'Anting','master':'Dalubhasa','skill':'Kasanayan',
 'reset':'Ibalik','multi':'Marami','shot':'Putok','other':'Iba','world':'Mundo','tome':'Aklat',
 'penetration':'Pagtagos','pet':'Alaga','phoenix':'Fenix','power':'Lakas','premium':'Pribado',
 'package':'Pakete','rageful':'Galit','recovery':'Pagbangon','spiral':'Espiral','starfall':'BituinLagas',
 'teleport':'Teleport','ally':'Kakampi','triple':'Triple','twisting':'Pilipit','vampiric':'Bampira',
 'vengeance':'Paghihiganti','wolf':'Lobo','altar':'Dambana','flameberge':'Flamberge','flail':'MasoKadena',
 'arquebus':'Arkabus','berdysh':'Bardysh','bowr':'Pana','ale':'Ale','aileen':'Aileen','hades':'Hades','iris':'Iris',
 'monster':'Halimaw','mana':'Mana','sd':'SD','maple':'Maple','panda':'Panda','rudolf':'Rudolf','unicorn':'Unicorn',
 'ale':'Serbesa','lunge':'Bulusok',
 'platina':'Platina','gorgon':'Gorgon','jack':'Jack','o':'O','chaos':'Chaos','figurine':'Estatwa',
}
PL_WORD2={
 'aqua':'Wodny','beam':'Promień','rays':'Promienie','castle':'Zamek','demon':'Bies','slasher':'Siekacz',
 'dragon':'Smoczy','dungeon':'Loch','falchion':'Falcjon','flameberge':'Flamberge','mana':'Many',
 'pollution':'Zanieczyszczenie','santa':'Święty','fortune':'Fortuna','quickness':'Szybkość',
 'kantururelics':'RelikwieKanturu','kanturu':'Kanturu','relics':'Relikwie','ruins':'Ruiny',
 'losttower':'ZagubionaWieża','peaceswamp':'BagnoSpokoju','swamp':'Bagno','peace':'Spokoju',
 'stone':'Kamienny','golem':'Golem','blood':'Krwi','howling':'Wycie','burn':'Spalenie',
}
def gen(lang):
    if lang=='id':
        phrase={**ID_PHRASE,**ID_PHRASE_ADD}
        word={**ID_WORD,**ID_SLOT,**ID_ADD,**ID_WORD2}; slot=ID_SLOT; hf=True
    elif lang=='tl':
        phrase={**ID_PHRASE,**ID_PHRASE_ADD,**TL_PHRASE}
        word={**ID_WORD,**ID_SLOT,**ID_ADD,**TL_WORD,**TL_SLOT,**TL_WORD2}; slot=TL_SLOT; hf=True
    elif lang=='pl': phrase,word,slot={},{**PL_WORD,**PL_WORD2},PL_SLOT; hf=False
    real=json.load(open(os.path.join(W,'real_gap_ids.json')))[lang]
    res=[]; issues=[]
    for i in real:
        en=STR[i]['en']; bud=STR[i]['budget']; kind=STR[i]['kinds'][0]
        if (lang,i) in OVERRIDE: t=OVERRIDE[(lang,i)]
        elif lang=='ja' and i in JA_SENT: t=JA_SENT[i]
        else:
            t=translate_words(en,phrase,word,slot,headfirst=hf)
        # cleanup possessives for setoption
        if kind=='setoption':
            t=re.sub(r"['’]s$",'',t)
            t=re.sub(r"['’]\s*$",'',t).strip()
        nb=len(t.encode('utf-8'))
        if nb>bud: issues.append((i,'budget %d>%d'%(nb,bud),en,t))
        if t.strip().casefold()==en.strip().casefold() and re.search(r'[a-z]',en):
            # still english (proper noun) - acceptable for known proper nouns, flag for review
            issues.append((i,'kept-en',en,t))
        res.append({'id':i,'tx':t})
    os.makedirs(os.path.join(W,'out_data_gap',lang),exist_ok=True)
    json.dump({'results':res},open(os.path.join(W,'out_data_gap',lang,'%s_gap_p1.json'%lang),'w',encoding='utf-8'),ensure_ascii=False)
    print('==',lang,'generated',len(res))
    over=[x for x in issues if x[1].startswith('budget')]
    kept=[x for x in issues if x[1]=='kept-en']
    print('  overflow',len(over),'kept-english',len(kept))
    for x in over[:25]: print('   OV',x)
    return over,kept

if __name__=='__main__':
    langs=sys.argv[1:] or ['id','tl','pl','ja']
    rep={}
    for L in langs: rep[L]=gen(L)
