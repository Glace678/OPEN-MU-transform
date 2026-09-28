#!/usr/bin/env python3
# -*- coding: utf-8 -*-
import os as _os
W = _os.path.dirname(_os.path.abspath(__file__))
"""Patch additional translations for missing IDs."""
import json
import os

INPUT = os.path.join(W, 'in_data', 'pl', 'g1.json')
OUTPUT = os.path.join(W, 'out_data', 'pl', 'g1.json')

with open(INPUT, "r", encoding="utf-8") as f:
    data = json.load(f)
strings = data["strings"]

# Load existing translations from the main script
import importlib.util
spec = importlib.util.spec_from_file_location("trans", os.path.join(W, 'translate_g1.py'))
trans_module = importlib.util.module_from_spec(spec)
# Don't execute the whole script, just build TR dict manually
spec.loader.exec_module(trans_module)
TR = trans_module.TR

# Fix semicolon mismatches from earlier
TR[1169] = "Najpierw musisz wyeliminować żołnierzy Kunduna atakujących wejście do świątyni, a potem pozbyć się wrogów rzucających zaklęcia na zapieczętowany kamień. Dam ci drugi cel, gdy skończysz pierwszy i wrócisz do mnie. Oto pierwszy cel; idź do Kalima 6 i wyeliminuj 30 Aegis i 20 Rogue Centurionów – żołnierzy Kunduna. (Można w drużynie)"

# Remove incorrect semicolons from player response strings
TR[1615] = "Starożytna moc w Świątyni Iluzji? Oczywiście, że pomogę!"
TR[1635] = "Tak więc chciałeś zostać bohaterem. Ha! Bycie bohaterem to nie tylko sława i fortuna. To ciężka praca i poświęcenie. Ale jeśli naprawdę chcesz... Możesz zostać moim uczniem. Czy chcesz?"
TR[1636] = "Zostać twoim uczniem? Zdecydowanie!"
TR[1664] = "Odpowiedź to... serce? Odpowiedź to... wola? Odpowiedź to... umysł?"
TR[1706] = "Rada i prezenty? Chętnie!"
TR[1727] = "Ulepszanie ekwipunku? Powiedz mi więcej."
TR[1748] = "Healing Potions i Mana Potions... Dziękuję za informację!"
TR[1882] = "Dobrze, zapisz mnie! Hmm, 1 milion... nie mam teraz tyle."

# ============================================================
# Additional missing translations (139 IDs)
# ============================================================

TR.update({
    1444: "Dobra robota. Energia szaleństwa jest bezpiecznie zamknięta, skoro Necroni i Death Centurionowie zostali wyeliminowani. Ale ta część, którą już wchłonęli, została przeniesiona do Kunduna, więc musimy go zaatakować bezpośrednio. Czy możesz nam pomóc? (Lv. 350 - 400)",
    1456: "Wielki Czarodziej Etramu przysiągł zniszczyć Illusion Sorcery i utworzył armię, by podbić jego naśladowców. Pod dowództwem Etramu przymierze zaangażowało się w walkę z naśladowcami Illusion Sorcery z ogromną siłą. Mimo że zostali zepchnięci aż do Illusion Temple, naśladowcy nadal stawiali opór. Gdy bitwa trwała, Lev zniknął, pozostawiając naśladowców Illusion Sorcery w zamieszaniu. Ostatecznie wszyscy zginęli z rąk przymierza na terenie świątyni.",
    1470: "Świetnie! Wreszcie mam szansę pokazać, na co mnie stać.",
    1511: "Byłeś w Chaos Castle? Jeśli nie, będzie to doskonała okazja, by dowieść swojej siły. Może wysłuchasz, co mam do powiedzenia, a potem zdecydujesz? (Postać podstawowa: Poziom 50-119, Postać zaawansowana: Poziom 30-99)",
    1512: "Ukończyłeś dzisiejszą prośbę? Pokaż mi więc Świadectwo Ukończenia Prośby Lv. 1.",
    1513: "Słyszałeś kiedykolwiek o barierze Blood Castle? Czy chcesz usłyszeć prośbę Archanioła o pomoc w obronie bariery Blood Castle i dołączyć do walki o zamek? (Postać podstawowa: Poziom 15-80, Postać zaawansowana: Poziom 10-60)",
    1515: "Słyszałeś kiedykolwiek o Illusion Temple i starożytnych relikwiach ukrytych tam? To miejsce, gdzie podróżnicy mogą znaleźć zarówno bogactwo, jak i sławę. Może wysłuchasz, co mam do powiedzenia, a potem zdecydujesz? (Wszystkie postacie: Poziom 220-270)",
    1516: "Słyszałeś kiedykolwiek o Illusion Temple i starożytnych relikwiach ukrytych tam? To miejsce, gdzie podróżnicy mogą znaleźć zarówno bogactwo, jak i sławę. Może wysłuchasz, co mam do powiedzenia, a potem zdecydujesz? (Wszystkie postacie: Poziom 271-320)",
    1517: "Skoro Secrarium upatrzył sobie moc narodzin drzewa, zepscił Drzewo Elfów i przekształcił je w Drzewo Nieprawości, które rodziło potwory. Horda potworów urosła do astronomicznych rozmiarów, a królestwa kontynentu MU połączyły siły, by uwięzić drzewo nieprawości w przestrzeni nicości.",
    1518: "Posiadanie zaufanych towarzyszy u boku robi całą różnicę, jeśli chodzi o zadania Blood Castle. Jednakże wciąż będzie ci trudno zdobyć Invisibility Cloak odpowiedni dla twojego poziomu. Mam zadanie, za które otrzymasz przedmiot potrzebny do wejścia do Blood Castle. Czy przyjmiesz to zadanie? (Postać podstawowa: Poziom 331-400, Postać zaawansowana: Poziom 311-400)",
    1529: "Wysłuchaj, co ma do powiedzenia Shadow Phantom Soldier, i podejmij wyzwanie Chaos Castle. Jeśli przeżyjesz w Chaos Castle, możesz odebrać pokaźną nagrodę.",
    1530: "Wysłuchaj, co ma do powiedzenia Shadow Phantom Soldier, i podejmij wyzwanie Devil Square. Trening w Devil Square przyniesie ci cenną nagrodę.",
    1531: "Wysłuchaj, co ma do powiedzenia Shadow Phantom Soldier, i podejmij wyzwanie Illusion Temple. Wygrana w Illusion Temple przyniesie ci cenną nagrodę.",
    1542: "Pomóż oddziałowi zwiadowczemu....",
    1545: "Oto List Prośby dla ciebie. Kliknij list prawym przyciskiem myszy, by sprawdzić informacje. Pamiętaj, że nie otrzymasz zwrotu kaucji w wysokości 500 000 Zen, jeśli zrezygnujesz z zadania. Musisz też przynieść 'Świadectwo Ukończenia Prośby', by dostać zwrot. Jeszcze raz: musisz nacisnąć (T) i kliknąć przycisk 'Rozpocznij Zadanie' na dole, by ponownie sprawdzić informacje po zamknięciu okna. Do widzenia więc.",
    1546: "Proszę bardzo.",
    1547: "Proszę.",
    1548: "Oto twoje pierwsze pytanie.;Jak handlować z postacią, która stoi przed tobą?",
    1551: "Heroiczny Duch Walki",
    1552: "Heroiczny Umysł",
    1553: "Heroiczne Cechy",
    1554: "Heroiczna Wytrwałość",
    1555: "Cześć, jestem Priestess Beina, strażniczka tej Podwodnej Świątyni. Zapieczętowany kamień stał się silniejszy dzięki twoim staraniom, ale armia Kunduna też teraz silnieje. Zbadałyśmy zapieczętowany kamień i odkryłyśmy, że armia Kunduna absorbuje jego energię, gdy dusza Etramu odpoczywa. Proszę, pomóż nam i zamknij energię szaleństwa w zapieczętowanym kamieniu. (Lv. 350 - 400)",
    1556: "Cześć, jestem Priestess Beina, strażniczka tej Podwodnej Świątyni. Ta świątynia jest pierwszym celem Kunduna, skoro Etramu trzyma swoje ciało w zapieczętowanym kamieniu. Zawsze brakuje nam wojowników, więc proszę o pomoc gildię najemników. Czy sprawdzisz prośbę? (Lv. 350 - 400)",
    1561: "Wysokiego rzędu potwory głęboko w Lost Tower przeprowadziły zaskakujący atak na nasz oddział zwiadowczy. Zgodnie z rozkazami kapitana o odwecie, tworzymy teraz siłę karną. Czy przyjmiesz to zadanie? (Poziom 206-209)",
    1565: "Hmm, wolny najemnik?",
    1566: "Hmm... to nie brzmi tak źle.",
    1567: "Hmm... jesteś mądrym wojownikiem. Idź teraz i osiągnij wielkie rzeczy w imieniu Gens Vanert, wiedząc, że zawsze będę tu, by ci pomóc.",
    1568: "Hmm... najpierw naciśnij M, a następnie...",
    1569: "Hmm... test...",
    1570: "Czekaj, znajdę zadanie odpowiednie dla ciebie. Proszę poczekaj chwilę. (Poziom 260-289)",
    1581: "Jak sprawdzić współrzędne?",
    1582: "Jak to zdobyć?",
    1583: "Jak się masz? Jestem Gregory, Gens Duprian Steward. Jaki masz interes z naszym gens?",
    1584: "Jak się masz? W imieniu mistrza naszego klanu dziękuję ci za odpowiedź na wezwanie klanu. Osobiście nie lubię zlecać tej roboty wędrownym najemnikom, więc poprosiłem, by wysłać kogoś z klanu. Mam nadzieję, że cię to nie uraziło. (Lv. 350 - 400)",
    1585: "Ile kosztuje opłata rejestracyjna i kaucja?",
    1587: "Jednakże podróże takie jak ta z pewnością będą niebezpieczne.;Jeśli chcesz, możemy ci zapewnić prosty samouczek. Co mówisz?",
    1588: "Wytrop 10 pająków z okolic miasta, a dam ci nagrodę.",
    1590: "Polowanie na marne pająki to nic dla mnie. (Odrzuć)",
    1591: "Polowanie na potwory....",
    1592: "Polowanie na potwory przynosi punkty EXP, upuszczone przedmioty i Zeny.",
    1601: "Przyjmuję twoje zadanie. (Przyjmij)",
    1602: "Już to wiedziałem.",
    1603: "Już to wiedziałem. Co jest następne zadanie?",
    1604: "Jeszcze nie jestem gotowy. (Odrzuć)",
    1605: "Prawie widzę koniec! (Przyjmij)",
    1606: "Nie mogę tego przegapić. (Przyjmij)",
    1609: "Nie mam teraz czasu. (Odrzuć)",
    1610: "Nie mam teraz czasu. Zrobię to następnym razem.",
    1611: "Nie mam czasu. Przepraszam. (Odrzuć)",
    1612: "Nie mam teraz czasu. Wrócę później. (Odrzuć)",
    1613: "Nie wiem, jak ta historia może mi pomóc. (Odrzuć)",
    1616: "Nie chcę decydować teraz. Po prostu je opisz.",
    1617: "Poddaję się. Dalsza droga to dla mnie za wiele. (Odrzuć)",
    1618: "Rozumiem!",
    1619: "Chyba Lugard został oszukany przez Secrarium. Jak mogę wziąć udział?",
    1620: "Mam zadanie polowania na potwory, które zaprowadzi cię głębiej do Lochu, niż kiedykolwiek wcześniej. Czy przyjmiesz to zadanie? (Poziom 101-110)",
    1623: "Mam zadanie, które zaprowadzi cię głęboko do Lochu. Pojawiły się doniesienia o potworach próbujących wydostać się na powierzchnię pod dowództwem Gorgonów. Chcę, byś pokonał te potwory. (Poziom 111-120)",
    1624: "Mam proste zadanie, za które otrzymasz Scroll of Blood – przedmiot dający dostęp do Illusion Temple. Przyjmujesz? (Wszystkie postacie: Poziom 321-350)",
    1625: "Mam proste zadanie, za które otrzymasz zaproszenie dające dostęp do Devil Square. Czy przyjmiesz to zadanie? (Postać podstawowa: Poziom 181-230, Postać zaawansowana: Poziom 161-210)",
    1626: "Nie mam teraz czasu. (Odrzuć)",
    1627: "Mam duże doświadczenie.;Każdy swoje!",
    1630: "Dobrze to znam. Słyszałem, że potrzebny jest przedmiot, by wziąć udział w wydarzeniu....",
    1631: "Nauczyłem się umiejętności!",
    1632: "Najpierw musisz przyjąć małe zadanie jako test. Dasz radę? Oczywiście za ukończenie czeka cię rozsądna nagroda.",
    1633: "Nie mam ani umiejętności, ani wojowników potrzebnych do tego zadania. Daj mi więcej czasu.",
    1634: "Żałuję, że nie możemy ci jakoś pomóc.;Oto, weź przynajmniej ten prezent. Mam nadzieję, że następnym razem, gdy się spotkamy, przede mną stanie wielki wojownik.;Miłego czasu na zewnątrz.",
    1637: "Widzę, że pomyślnie ukończyłeś swoje pierwsze polowanie! Było trudne? Polowanie na potwory przynosi punkty EXP, upuszczone przedmioty i Zeny.;Możesz podnosić przedmioty upuszczone przez pokonane potwory za pomocą myszy lub automatycznie, naciskając spację.",
    1638: "Rozumiem.",
    1639: "Rozumiem. Jak wejść?",
    1640: "Po prostu nie mogę iść dalej. (Odrzuć)",
    1641: "Myślę, że mam już wystarczająco dużo odwagi. (Odrzuć)",
    1643: "Jestem pewien, że już całkiem dobrze znasz Devil Square. Jednakże zdobycie zaproszenia do Devil Square pozostaje trudnym zadaniem. Na szczęście mam zadanie, za które otrzymasz zaproszenie. Czy przyjmiesz to zadanie? (Postać podstawowa: Poziom 231-280, Postać zaawansowana: Poziom 211-260)",
    1644: "Jestem pewien, że już trochę znasz Blood Castle. Powinieneś teraz skupić się na budowaniu więzi między tobą a twoimi towarzyszami do tego stopnia, byście mogli na sobie polegać bezwarunkowo.",
    1645: "Jestem pewien, że już trochę znasz Chaos Castle. Jednakże wciąż powinno być ci trudno zdobyć potrzebny przedmiot. Mam dla ciebie zadanie. Takie, dzięki któremu możesz zdobyć przedmioty potrzebne w Chaos Castle.... Przyjmujesz? (Postać podstawowa: Poziom 240-299, Postać zaawansowana: Poziom 220-279)",
    1646: "Jestem pewien, że już trochę znasz Chaos Castle. Wydarzenie będzie teraz jeszcze bardziej wymagające, ponieważ staniesz twarzą w twarz z potężniejszymi graczami i zepsutymi gwardzistami.",
    1647: "Jestem pewien, że już trochę znasz Devil Square. Zostaniesz teraz przeniesiony w miejsce, gdzie pojawiają się jeszcze potężniejsze potwory. Nie ma jednak powodów do obaw. Możesz spodziewać się szybkiego wzrostu, o ile ty i twoi towarzysze będziecie przeprowadzać skoordynowane ataki.",
    1648: "Jestem pewien, że już trochę znasz Illusion Temple. Zostaniesz teraz przeniesiony w miejsce, gdzie pojawiają się jeszcze potężniejsze potwory. Nie ma jednak powodów do obaw. Możesz spodziewać się szybkiego wzrostu, o ile ty i twoi towarzysze nauczycie się ze sobą współpracować.",
    1650: "Rozumiem.",
    1651: "Rozumiem. Zrobię, co w mojej mocy.",
    1652: "Chcę być Elfem skupiającym się na obronie!",
    1653: "Chcę być Elfem skupiającym się na magii!",
    1654: "Chcę usłyszeć więcej. (Przyjmij)",
    1655: "Dam ci zadanie, za które otrzymasz przedmiot potrzebny do wejścia na wydarzenie. Czy przyjmiesz to zadanie? (Postać podstawowa: Poziom 120-179, Postać zaawansowana: Poziom 100-159)",
    1657: "Przeprowadzę cię przez rozgrywkę w MU.",
    1658: "Pomogę ci. Powiedz, co mam zrobić. (Przyjmij)",
    1659: "Poinformuję cię, gdy pojawi się prośba na twoim poziomie. Dziękuję.",
    1660: "Zapisuję się. (Przyjmij)",
    1661: "Nauczę cię podstaw polowania i sterowania przez prosty samouczek.;Nie jest to trudne i jestem pewien, że skończysz go w mgnieniu oka.",
    1662: "Nauczę cię polowania i poleceń sterowania przez prosty samouczek.",
    1665: "Wyjaśnię to bardziej szczegółowo. Co jeszcze chcesz wiedzieć?",
    1666: "Dam ci wtedy pełną odprawę, gdy będziesz gotowy. Niech łaska Boga będzie z tobą. (Zadanie zostało anulowane)",
    1667: "Dam ci wtedy pełną odprawę, gdy będziesz gotowy. Niech łaska Boga będzie z tobą. (Zadanie zostało anulowane.)",
    1668: "Od razu ruszam do Vulcanus. (Przyjmij)",
    1669: "Po prostu wezmę prezenty od królowej bez zdawania testu.",
    1671: "Przetestuję cię, by sprawdzić, czy posiadasz podstawową wiedzę potrzebną do podróżowania po imperium MU, i dam ci prezenty od królowej, jeśli zdasz.",
    1672: "Zawsze chętnie poluję na potwory....",
    1673: "Jestem zajęty teraz. Wrócę później. (Odrzuć)",
    1674: "Cieszę się, że możemy pomóc. Nie trudno nas znaleźć.;Po prostu poszukaj Shadow Phantom Soldier w którymkolwiek z miast i z nim porozmawiaj.",
    1675: "Jestem tu po twoją prośbę.",
    1676: "Jeszcze nie jestem gotowy. Proszę czekaj. (Odrzuć)",
    1679: "Przepraszam. Nie mam teraz czasu. (Odrzuć)",
    1680: "Przepraszam. Nie mam czasu. (Odrzuć)",
    1681: "Jestem pewien, że dobrze znasz Kunduna. Najsłabsze i najbardziej niezsynchronizowane potwory armii Kunduna często atakują i grabią cywilów tuż za miastem. Chciałem cię poprosić o wyeliminowanie tych grabieżczych potworów.... Czy przyjmiesz to zadanie? (Poziom 15-25)",
    1682: "Jestem pewien, że masz za sobą długi szlak pokonanych potworów. Jednakże podróżnicy bardzo różnią się pod względem swoich umiejętności. Jeśli nie masz pewności co do własnych umiejętności, udowodnienie siebie w Chaos Castle powinno dać ci jasność. Mam dla ciebie zadanie. Takie, dzięki któremu możesz zdobyć przedmioty potrzebne w Chaos Castle.... Przyjmujesz? (Wszystkie postacie: Master Level)",
    1683: "Jestem zbyt zajęty, by teraz pomóc.",
    1701: "Ice Walkerowie atakują i osłabiają barierę utworzoną, by oddzielić La Cleon od Devias. Idź do La Cleon i pokonaj 120 Ice Walkerów. Przyjdź do mnie, gdy to zrobisz, by otrzymać Devil's Invitation + 6 jako nagrodę. To silni przeciwnicy – powinieneś na nie polować z pomocą innych.",
    1707: "Jeśli masz wyposażoną broń nasyconą umiejętnością, możesz ją aktywować, przypisując ją w oknie Umiejętności.;Widzisz ponumerowane sloty na dole, na środku ekranu? Kliknij ikonę zaznaczoną na zielono, po prawej stronie slotu piątego.",
    1708: "Jeśli nie chcesz otrzymywać propozycji drużyny podczas polowania, po prostu wpisz 'Party off' w oknie czatu. By znów zacząć je przyjmować, wpisz 'Request on' w oknie czatu.",
    1709: "Jeśli znajdziesz się daleko od miasta lub zostaniesz zaskoczony podczas polowania, Town Portal Scroll może się bardzo przydać.",
    1710: "Jeśli opuścisz gens, twój wkład zostanie zresetowany.;Czy nadal chcesz opuścić?",
    1711: "Jeśli chcesz dowiedzieć się, kiedy możesz wejść, po prostu najedź myszą na Armor of Guardsman. Jeśli chcesz wejść, po prostu kliknij prawym przyciskiem myszy na zbroi o wyznaczonej porze. Przyjdź do mnie, gdy będziesz miał czas, a dam ci zadanie, za które otrzymasz przedmiot potrzebny do wejścia na wydarzenie.",
    1722: "Zaklęcia Iluzji, fundament Illusion Sorcery, zostały odkryte przez Archeolożkę o imieniu Mirage. Mirage uznała moc zaklęć za zbyt niebezpieczną i je zapieczętowała. Jego partner Lev ukradł jednak zaklęcia, by prać ludziom mózgi i ustanowić Illusion Sorcery. Illusion Sorcery stopniowo rosło i przynosiło chaos na kontynencie.",
    1723: "Adaptacja do Illusion Temple (1)",
    1724: "Adaptacja do Illusion Temple (2)",
    1725: "Adaptacja do Illusion Temple (3)",
    1728: "Wyzwanie Illusion Temple (3)",
    1730: "Illusion Temple to miejsce, gdzie możesz zdobyć przedmioty i ekwipunek na szybki wzrost. Jeśli chcesz posiąść takie przedmioty i ekwipunek, zapytaj Shadow Phantom Soldier o Illusion Temple.",
    1731: "Illusion Temple to miejsce, gdzie możesz znaleźć zarówno bogactwo, jak i sławę. Jeśli podejmiesz wyzwanie świątyni, możesz zdobyć coś niezbędnego do twojego treningu. Czy chcesz podjąć wyzwanie? (Wszystkie postacie: Poziom 271-320)",
    1732: "Illusion Temple to miejsce, gdzie możesz znaleźć zarówno bogactwo, jak i sławę. Jeśli podejmiesz wyzwanie świątyni, możesz zdobyć coś niezbędnego do twojego treningu. Czy chcesz podjąć wyzwanie? (Wszystkie postacie: Poziom 321-350)",
    1734: "Illusion Temple to miejsce, gdzie możesz znaleźć zarówno bogactwo, jak i sławę. Jeśli podejmiesz wyzwanie świątyni, możesz zdobyć coś niezbędnego do twojego treningu. Czy chcesz podjąć wyzwanie? (Wszystkie postacie: Master Level)",
    1735: "Illusion Temple...",
    1743: "W Blood Castle będziesz walczył z czasem. Dobra zbroja i broń oczywiście pomogą ci ukończyć zadanie, ale ponad wszystko liczy się tam solidarność twojej drużyny. Czy przyjmiesz misję Archanioła? (Wszystkie postacie: Master Level)",
    1744: "W Blood Castle będziesz walczył z czasem. Dobra zbroja i broń oczywiście pomogą ci ukończyć zadanie. Ale ponad wszystko liczy się tam solidarność twojej drużyny. Czy przyjmiesz misję Archanioła? (Postać podstawowa: Poziom 231-280, Postać zaawansowana: Poziom 211-260)",
    1745: "W Blood Castle będziesz walczył z czasem. Dobra zbroja i broń oczywiście pomogą ci ukończyć zadanie. Ale ponad wszystko liczy się tam solidarność twojej drużyny. Czy przyjmiesz misję Archanioła? (Postać podstawowa: Poziom 131-180, Postać zaawansowana: Poziom 111-160)",
    1746: "W Blood Castle będziesz walczył z czasem. Dobra zbroja i broń oczywiście pomogą ci ukończyć zadanie. Ale ponad wszystko liczy się tam solidarność twojej drużyny. Czy przyjmiesz misję Archanioła? (Postać podstawowa: Poziom 15-80, Postać zaawansowana: Poziom 10-60)",
    1749: "W Blood Castle będziesz walczył z czasem. Dobra zbroja i broń oczywiście pomogą ci ukończyć zadanie. Ale ponad wszystko liczy się tam solidarność twojej drużyny. Czy przyjmiesz misję Archanioła? (Postać podstawowa: Poziom 331-400, Postać zaawansowana: Poziom 311-400)",
    1750: "W Blood Castle będziesz walczył z czasem. Dobra zbroja i broń oczywiście pomogą ci ukończyć zadanie. Ale ponad wszystko liczy się tam solidarność twojej drużyny. Czy przyjmiesz misję Archanioła? (Postać podstawowa: Poziom 81-130, Postać zaawansowana: Poziom 61-110)",
    1751: "W MU można łączyć różne przedmioty, by tworzyć nowe. Właśnie próbowanie różnych kombinacji przedmiotów dodaje zupełnie nowego wymiaru do MU.;Kogo powinieneś szukać, by łączyć przedmioty?",
    1752: "W świecie MU możesz użyć przycisku M, by podróżować z łatwością, pod warunkiem że twój poziom jest wystarczająco wysoki i masz wystarczająco dużo pieniędzy na cel podróży. Odkrywanie obszarów odpowiednich dla twojego poziomu i polowanie tam też może być bardzo fajne.",
    1857: "Lądowa Trasa Dostawcza!",
    1879: "Iron Riderzy przeprowadzają partyzanckie ataki na nasze siły stacjonujące w Kanturu Ruins. Wyeliminuj tych Iron Riderów i złagodź stres naszych członków gens.",
    1883: "Czy więź twojej drużyny Blood Castle jest nadal silna? Jeśli nie, sugeruję powrót do Blood Castle, by ją wzmocnić. Mam zadanie, za które otrzymasz przedmiot potrzebny do wejścia do Blood Castle. Czy przyjmiesz to zadanie? (Postać podstawowa: Poziom 231-280, Postać zaawansowana: Poziom 211-260)",
    1884: "Czy jest coś, na co powinienem uważać?",
    1885: "Czy jest coś, co powinienem wiedzieć o Illusion Temple?",
    1886: "Czy jest coś, co powinienem wiedzieć o Devil Square?",
    1887: "Teraz wszystko ma sens.",
    1890: "Wygląda na to, że Cursed Liches dręczą mieszkańców Elveland swoimi ciemnymi zaklęciami i klątwami. Wytrop 40 tych Cursed Liches, a dam ci Devil's Invitation +1 jako nagrodę. Sugeruję współpracę z innymi dla szybszych efektów.",
    1891: "Wygląda na to, że masz potężnych wojowników, którzy chcą walczyć u twego boku. Stawienie czoła wyzwaniom Blood Castle razem z pewnością wzmocni twoją więź z towarzyszami. Czy wrócisz do Blood Castle i wykonasz kolejną misję Archanioła? (Wszystkie postacie: Master Level)",
})

# ============================================================
# Build output
# ============================================================

results = []
missing = []
semicolon_errors = []
for s in strings:
    sid = s["id"]
    if sid in TR:
        tx = TR[sid]
        # verify semicolon count
        orig_sc = s["en"].count(";")
        tx_sc = tx.count(";")
        if orig_sc != tx_sc:
            semicolon_errors.append((sid, orig_sc, tx_sc))
        results.append({"id": sid, "tx": tx})
    else:
        missing.append(sid)
        results.append({"id": sid, "tx": s["en"]})  # fallback

print(f"Total strings: {len(strings)}")
print(f"Translated: {len(results) - len(missing)}")
print(f"Missing translations: {len(missing)}")
if missing:
    print(f"Missing IDs: {sorted(missing)}")
if semicolon_errors:
    print(f"\nSemicolon mismatches ({len(semicolon_errors)}):")
    for sid, orig, tx in semicolon_errors:
        print(f"  id {sid}: orig={orig}, tx={tx}")

output = {"results": results}

os.makedirs(os.path.dirname(OUTPUT), exist_ok=True)
with open(OUTPUT, "w", encoding="utf-8") as f:
    json.dump(output, f, ensure_ascii=False, indent=1)

print(f"\nWritten to {OUTPUT}")

# verify
with open(OUTPUT, "r", encoding="utf-8") as f:
    check = json.load(f)
print(f"Verification: {len(check['results'])} results, valid JSON")
print(f"First result: id={check['results'][0]['id']}, tx={check['results'][0]['tx'][:50]}...")
print(f"Last result: id={check['results'][-1]['id']}, tx={check['results'][-1]['tx'][:50]}...")
