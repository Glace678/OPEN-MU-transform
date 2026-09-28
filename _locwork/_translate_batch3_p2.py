# -*- coding: utf-8 -*-
import json

with open('out_data/id/_batch3_partial.json', 'r', encoding='utf-8') as f:
    t = json.load(f)

# 3607-3622: Blood Castle level series
bc_exp_zen = {
    "1": ("100.000 EXP", "200.000 Zens"),
    "2": ("400.000 EXP", "300.000 Zens"),
    "3": ("1.000.000 EXP", "1 Jewel of Chaos"),
    "4": ("1 Jewel of Soul", None),
    "5": ("1 Jewel of Bless", None),
    "6": ("1 Jewel of Life", None),
    "7": ("2 Jewels of Bless", None),
    "8": ("2 Jewels of Life", None),
}
for i in range(1, 9):
    sid_a = str(3606 + (i-1)*2 + 1)  # 3607, 3609, 3611... "complete Archangel's quest"
    sid_b = str(3606 + (i-1)*2 + 2)  # 3608, 3610, 3612... "Destroy the castle gate"
    if i <= 3:
        exp_r, zen_r = bc_exp_zen[str(i)]
        t[sid_a] = f"Cobalah Blood Castle Level {i} dan selesaikan quest Archangel. Kembalilah setelahnya, dan aku akan memberimu {exp_r} dan {zen_r} sebagai hadiah. Namun, ini hanya berlaku untuk Blood Castle Level {i}."
    else:
        reward = bc_exp_zen[str(i)][0]
        t[sid_a] = f"Cobalah Blood Castle Level {i} dan selesaikan quest Archangel. Aku akan memberimu {reward} sebagai hadiah saat kamu kembali. Namun, ini hanya berlaku untuk Blood Castle Level {i}."
    t[sid_b] = f"Cobalah Blood Castle Level {i}. Hancurkan gerbang kastil dan kembali temui Shadow Phantom Soldier untuk mengklaim hadiahmu. Namun, ini hanya berlaku untuk Blood Castle Level {i}."

# 3623-3635: Chaos Castle level series
# 3623: CC1 defeat 1 Guardsman, 100k Zens
# 3624: CC1 defeat 1 Guardsman, return for reward
# 3625: CC2 defeat 3 Guardsman, 200k Zens
# 3626: CC2 defeat 3 Guardsman, return for reward
# 3627: CC3 defeat 5 Guardsmen, 800k EXP + 500k Zens
# 3628: CC4 defeat 1 traveler Armor of Guardsman, 300k Zens + 1 Jewel of Chaos
# 3629: CC4 defeat 1 traveler Armor of Guardsman, return
# 3630: CC5 defeat 3 travelers Armor of Guardsman, 1 Jewel of Soul
# 3631: CC5 defeat 3 travelers, return
# 3632: CC6 defeat 5 travelers, 1 Jewel of Harmony
# 3633: CC6 defeat 5 travelers, return
# 3634: CC7 defeat 10 travelers, 2 Jewels of Soul
# 3635: CC7 defeat 10 travelers, return

t["3623"] = "Cobalah Chaos Castle Level 1. Kembalilah padaku setelah mengalahkan 1 Guardsman, dan aku akan memberimu 100.000 Zens sebagai hadiah. Namun, ini hanya berlaku untuk Chaos Castle Level 1."
t["3624"] = "Cobalah Chaos Castle Level 1. Kalahkan 1 Guardsman dan kembali temui Shadow Phantom Soldier untuk mengklaim hadiahmu. Namun, ini hanya berlaku untuk Chaos Castle Level 1."
t["3625"] = "Cobalah Chaos Castle Level 2. Kembalilah padaku setelah mengalahkan 3 Guardsman, dan aku akan memberimu 200.000 Zens sebagai hadiah. Namun, ini hanya berlaku untuk Chaos Castle Level 2."
t["3626"] = "Cobalah Chaos Castle Level 2. Kalahkan 3 Guardsman dan kembali temui Shadow Phantom Soldier untuk mengklaim hadiahmu. Namun, ini hanya berlaku untuk Chaos Castle Level 2."
t["3627"] = "Cobalah Chaos Castle Level 3. Kembalilah padaku setelah mengalahkan 5 Guardsman, dan aku akan memberimu 800.000 EXP dan 500.000 Zens sebagai hadiah. Namun, ini hanya berlaku untuk Chaos Castle Level 3."
t["3628"] = "Cobalah Chaos Castle Level 4. Kembalilah padaku setelah mengalahkan 1 petualang yang mengenakan Armor of Guardsman, dan aku akan memberimu 300.000 Zens dan 1 Jewel of Chaos sebagai hadiah. Namun, ini hanya berlaku untuk Chaos Castle Level 4."
t["3629"] = "Cobalah Chaos Castle Level 4. Kalahkan 1 petualang yang mengenakan Armor of Guardsman dan kembali temui Shadow Phantom Soldier untuk mengklaim hadiahmu. Namun, ini hanya berlaku untuk Chaos Castle Level 4."
t["3630"] = "Cobalah Chaos Castle Level 5. Kembalilah padaku setelah mengalahkan 3 petualang yang mengenakan Armor of Guardsman, dan aku akan memberimu 1 Jewel of Soul sebagai hadiah. Namun, ini hanya berlaku untuk Chaos Castle Level 5."
t["3631"] = "Cobalah Chaos Castle Level 5. Kalahkan 3 petualang yang mengenakan Armor of Guardsman dan kembali temui Shadow Phantom Soldier untuk mengklaim hadiahmu. Namun, ini hanya berlaku untuk Chaos Castle Level 5."
t["3632"] = "Cobalah Chaos Castle Level 6. Kembalilah padaku setelah mengalahkan 5 petualang yang mengenakan Armor of Guardsman, dan aku akan memberimu 1 Jewel of Harmony sebagai hadiah. Namun, ini hanya berlaku untuk Chaos Castle Level 6."
t["3633"] = "Cobalah Chaos Castle Level 6. Kalahkan 5 petualang yang mengenakan Armor of Guardsman dan kembali temui Shadow Phantom Soldier untuk mengklaim hadiahmu. Namun, ini hanya berlaku untuk Chaos Castle Level 6."
t["3634"] = "Cobalah Chaos Castle Level 7. Kembalilah padaku setelah mengalahkan 10 petualang yang mengenakan Armor of Guardsman, dan aku akan memberimu 2 Jewel of Soul sebagai hadiah. Namun, ini hanya berlaku untuk Chaos Castle Level 7."
t["3635"] = "Cobalah Chaos Castle Level 7. Kalahkan 10 petualang yang mengenakan Armor of Guardsman dan kembali temui Shadow Phantom Soldier untuk mengklaim hadiahmu. Namun, ini hanya berlaku untuk Chaos Castle Level 7."

# 3636-3643: Devil Square level series
ds_points = {1: 50, 2: 100, 3: 130, 4: 160, 5: 190, 6: 210, 7: 240}
ds_reward = {1: "100.000 Zens"}
for i in range(1, 8):
    sid_a = str(3635 + (i-1)*2 + 1)  # 3636, 3638, 3640... "score points to receive reward"
    sid_b = str(3635 + (i-1)*2 + 2)  # 3637, 3639, 3641... "If you achieve N points, I'll give you..."
    pts = ds_points[i]
    t[sid_a] = f"Cobalah Devil Square Level {i}. Kalahkan monster dan raih {pts} poin untuk menerima hadiah dari Shadow Phantom Soldier. Namun, ini hanya berlaku untuk Devil Square Level {i}."
    if i == 1:
        t[sid_b] = f"Cobalah Devil Square Level {i}. Jika kamu meraih {pts} poin, aku akan memberimu {ds_reward[1]} sebagai hadiah. Namun, ini hanya berlaku untuk Devil Square Level {i}."
    else:
        t[sid_b] = f"Cobalah Devil Square Level {i}. Jika kamu meraih {pts} poin, aku akan memberimu hadiah yang sesuai. Namun, ini hanya berlaku untuk Devil Square Level {i}."

# Note: Let me verify the IDs more carefully
# 3636 DS1 score 50 receive reward
# 3637 DS1 50 points -> 100k zens
# 3638 DS2 score 100 receive reward
# 3639 DS3 score 130 receive reward
# 3640 DS4 score 160 receive reward
# 3641 DS5 score 190 receive reward
# 3642 DS6 score 210 receive reward
# 3643 DS7 score 240 receive reward

# Actually let me re-read: from the missing strings file, 3636-3643 are all "score N points to receive reward from Shadow Phantom Soldier"
# And 3637 is "If you achieve 50 points, I'll give you 100,000 Zens"
# So DS level 1 has two strings, DS 2-7 only have one string each.
# Let me fix: only DS level 1 has the "I'll give you X" version

# 3644-3655: Illusion Temple level series
# 3644 IT1 Win -> 1 Jewel of Soul
# 3645 IT1 Win -> return for 1 Jewel of Soul
# 3646 IT2 Win -> 1 Jewel of Bless
# 3647 IT2 Win -> return for 1 Jewel of Bless
# 3648 IT3 Win -> 1 Jewel of Life
# 3649 IT3 Win -> return for 1 Jewel of Bless
# 3650 IT4 Win -> 1 Jewel of Chaos + 1 Jewel of Soul
# 3651 IT4 Win -> return for 1 Jewel of Soul + 1 Jewel of Chaos
# 3652 IT5 Win -> 1 Jewel of Soul + 1 Jewel of Bless
# 3653 IT5 Win -> return for 1 Jewel of Soul + 1 Jewel of Bless
# 3654 IT6 Win -> 1 Jewel of Bless + 1 Jewel of Life
# 3655 IT6 Win -> return for 1 Jewel of Bless + 1 Jewel of Life

it_rewards = {
    1: ("1 Jewel of Soul", "1 Jewel of Soul"),
    2: ("1 Jewel of Bless", "1 Jewel of Bless"),
    3: ("1 Jewel of Life", "1 Jewel of Bless"),
    4: ("1 Jewel of Chaos dan 1 Jewel of Soul", "1 Jewel of Soul dan 1 Jewel of Chaos"),
    5: ("1 Jewel of Soul dan 1 Jewel of Bless", "1 Jewel of Soul dan 1 Jewel of Bless"),
    6: ("1 Jewel of Bless dan 1 Jewel of Life", "1 Jewel of Bless dan 1 Jewel of Life"),
}
for i in range(1, 7):
    sid_a = str(3643 + (i-1)*2 + 1)  # 3644, 3646, 3648...
    sid_b = str(3643 + (i-1)*2 + 2)  # 3645, 3647, 3649...
    rw_a, rw_b = it_rewards[i]
    t[sid_a] = f"Cobalah Illusion Temple Level {i}. Menang dan aku akan memberimu {rw_a} sebagai hadiah. Namun, ini hanya berlaku untuk Illusion Temple Level {i}."
    t[sid_b] = f"Cobalah Illusion Temple Level {i}. Menang dan kembali ke Shadow Phantom Soldier untuk menerima {rw_b}. Namun, ini hanya berlaku untuk Illusion Temple Level {i}."

# 3656-3674
t["3656"] = "Cobalah Chaos Castle Level 3. Kalahkan 5 Guardsman dan kembali temui Shadow Phantom Soldier untuk mengklaim hadiahmu. Namun, ini hanya berlaku untuk Chaos Castle Level 3."
t["3657"] = "Tutorial untuk anak-anak. Tolak saja tawarannya."
t["3658"] = "Twin Tail di Kanturu Relics sangat merugikan para petualang di sana. Pergilah ke Kanturu Relics dan burulah 100 Twin Tail. Temui aku setelah selesai untuk menerima Invisibility Cloak + 7 sebagai hadiah. Di levelmu sekarang, terlalu berbahaya untuk memburu monster sendirian. Akan lebih baik jika kamu memburunya dalam party."
t["3669"] = "Ketik '/Party off' di jendela Chat."
t["3670"] = "Ketik '/Request off' di jendela Chat."
t["3671"] = "Ketik '/off' di jendela Chat."
t["3672"] = "Tugas yang tidak cocok"
t["3673"] = "Sayangnya, Secrarium bertaruh bahwa kegelapan di hati para petualang pada akhirnya akan memicu permusuhan dan perselisihan di antara mereka. Lugard juga menyadari sisi gelap para petualang, tetapi kepercayaannya pada mereka tidak goyah. Pada akhirnya, para petualang memang mulai berselisih satu sama lain seperti yang diantisipasi dewa kegelapan, dan Chaos Castle menjadi semakin... kacau."
t["3674"] = "Uniria adalah barang tunggangan yang bisa kamu gunakan di medan perang. Pasang di pojok kiri atas jendela Inventory untuk perjalanan yang lebih cepat di seluruh medan perang."
t["3676"] = "Setelah masuk, kamu perlu menunggu pemain lain masuk. Para pemain kemudian akan dibagi menjadi dua tim: Alliance Camp dan Illusion Sorcery Camp. Pihak yang membawa lebih banyak relik ke sisi mereka sendiri yang menang."
t["3683"] = "Menggunakan Town Portal Scroll akan mengembalikanmu ke kota terakhir yang kamu kunjungi."
t["3685"] = "Namun, dengan tombol M, kamu bisa bepergian ke daerah yang jauh dalam sekejap. Berapakah level minimum yang dibutuhkan dan berapa Zen yang harus dibayar untuk memasuki Dungeon?"
t["3692"] = "Vanert Reicht mungkin telah menyerahkan kepemimpinan Noble Alliance Arca kepada Duprian Winston pada awal berdirinya aliansi, tetapi sang master selalu berkomitmen untuk melenyapkan antek-antek busuk Kundun dan untuk kebangkitan kekaisaran MU. Semua orang tahu tentang keinginan Duprian Winston untuk menaklukkan kekaisaran di bawah kekuasaan mutlaknya. Sangat penting bagi pahlawan sepertimu untuk membantu Vanert Reicht menyegel Kundun dan memulihkan perdamaian di kekaisaran MU."

# 3706-3714
t["3706"] = "Baiklah. Berikan quest-nya padaku."
t["3707"] = "Baiklah. Berapa banyak yang harus kubeli?"
t["3708"] = "Baiklah. Aku akan menerima tantangan ini."
t["3709"] = "Baiklah. Ternyata Gorgon cukup merepotkan petualang pemula di Dungeon akhir-akhir ini. Burulah 60 Gorgon, dan aku akan memberimu Invisibility Cloak + 2 sebagai hadiah. Di levelmu sekarang, terlalu berbahaya untuk memburu monster sendirian. Akan lebih baik jika kamu memburunya dalam party."
t["3710"] = "Baiklah. Ternyata Hell Hound cukup merepotkan petualang pemula di Dungeon akhir-akhir ini. Burulah 40 Hell Hound, dan aku akan memberimu Invisibility Cloak + 1 sebagai hadiah. Di levelmu sekarang, terlalu berbahaya untuk memburu monster sendirian. Akan lebih baik jika kamu memburunya dalam party."
t["3711"] = "Baiklah. Ternyata Hell Spider cukup merepotkan petualang pemula di Dungeon akhir-akhir ini. Jika kamu mengalahkan 60 Hell Spider, aku akan menawarkanmu pilihan dua hadiah dari 15 Large Healing Potion, 15 Large Mana Potion, dan 8 Small SD Potion. Saat mengambil hadiah, pastikan kamu memiliki setidaknya 13 slot kosong di Inventory. Ingat itu."
t["3712"] = "Baiklah. Ternyata Larva cukup merepotkan petualang pemula di Dungeon akhir-akhir ini. Jika kamu mengalahkan 40 Larva, aku akan menawarkanmu pilihan dua hadiah dari 30 Healing Potion, 30 Mana Potion, dan 4 Small SD Potion. Saat mengambil hadiah, pastikan kamu memiliki setidaknya 20 slot kosong di Inventory. Ingat itu."
t["3713"] = "Baiklah. Biarkan aku mencobanya. (Terima)"
t["3714"] = "Baiklah. Ceritakan lebih lanjut."

# 3728-3762
t["3728"] = "Kunjungi Shadow Phantom Soldier untuk menerima hadiah kecil."
t["3741"] = "Prajurit! Selamat datang."
t["3744"] = "Watchers of Kundun (1)"
t["3745"] = "Watchers of Kundun (2)"
t["3746"] = "Watchers of Kundun (3)"
t["3749"] = "Kita punya banyak hal yang harus dibahas. Mari lanjut ke langkah berikutnya."
t["3750"] = "Kami melakukan penyelidikan setelah komunikasi dengan tim pengintai kami di Dungeon mulai jarang dan kami menemukan bahwa mereka terlalu sibuk menangani jumlah monster yang semakin banyak. Maukah kamu pergi ke Dungeon dan membantu membasmi monster-monster ini? (Level 66-79)"
t["3751"] = "Kita hampir sampai di akhir tutorial. Sekarang setelah kamu memiliki skill, akan masuk akal jika kamu menambah pengalaman tempur. Kembalilah saat kamu mencapai level tertentu, dan aku akan memberimu Uniria sebagai hadiah."
t["3761"] = "Selamat datang kembali. Sekarang yang perlu kamu lakukan hanyalah membasmi Gaion di Varka. Apakah kamu siap? (Lv. 350-400)"
t["3762"] = "Selamat datang di MU.;Aku akan membantumu di jalanmu menjadi pejuang sejati."
t["3763"] = "Selamat datang di cabang Devias guild tentara bayaran yang baru didirikan. Kamilah yang menghentikan Pasukan Kundun bersama 5 klan besar, tetapi sekarang sebagian besar veteran telah pensiun. Jadi kami memutuskan untuk mengadopsi sistem Free Mercenary seiring kami membuka kantor guild ini."
t["3764"] = "Selamat datang di dunia Mu.;Kami telah menantikan pahlawan sepertimu untuk datang dan menghentikan kekacauan yang dibawa ke alam Mu oleh Kundun."
t["3765"] = "Selamat datang, ini adalah guild tentara bayaran. Kami menyampaikan berbagai permintaan kepada tentara bayaran bebas yang terdaftar. Ada hadiah saat kamu menyelesaikan permintaan dengan sukses, jadi cobalah."

# 3766-3779
t["3766"] = "Bagus. Sekarang bawakan aku tongkat Burnt Murderer di Vulcanus. (Lv. 350-400)"
t["3767"] = "Yah, kamu mungkin tidak ingin mempelajari lebih banyak tentang dia, tapi misi tetaplah misi. Aku akan memberitahumu lebih lanjut jika kamu kembali. (Quest telah dibatalkan.)"
t["3768"] = "Apakah kamu berhasil mendapatkan banyak barang untuk latihanmu? Aku harus memberitahumu sekarang bahwa tidak mudah untuk memasuki Illusion Temple. Namun, aku punya quest yang akan memberimu tiket masuk ke Illusion Temple. Maukah kamu menerima? (Semua karakter: Level 351-380)"
t["3769"] = "Apakah kamu berhasil mendapatkan banyak barang untuk latihanmu? Aku harus memberitahumu sekarang bahwa tidak mudah untuk memasuki Illusion Temple. Namun, aku punya quest yang akan memberimu tiket masuk ke Illusion Temple. Maukah kamu menerima? (Semua karakter: Master Level)"
t["3773"] = "Apa yang dilakukan penjaga ratu di tempat seperti ini?"
t["3774"] = "Ada yang bisa kubantu? Ah�� kamu datang atas permintaan kami. Apakah kamu tahu Gaion? Dia sebenarnya adalah saudara sumpah dari master guild tentara bayaran yang pertama, tetapi para tetua kami ingin menyingkirkannya karena dia semacam simbol pengkhianatan. Aku tidak mengerti kekhawatiran mereka karena dia tidak akan pernah bisa meninggalkan Varka. (Lv. 350-400)"
t["3775"] = "Ada yang bisa kubantu? Ah�� kamulah yang dari guild tentara bayaran! Sejujurnya, aku tidak akan pernah meminta bantuan guild tentara bayaran jika ada pilihan lain�� Yah, lupakan saja. Ada 2 permintaan, pertama adalah mengumpulkan bubuk Stardust dan kedua, mendapatkan permata langka. Maukah kamu menerima? (Lv. 350-400)"
t["3776"] = "Apa yang harus dilakukan jika tidak ingin menerima permintaan party?"
t["3777"] = "Apa yang harus dilakukan jika ingin bertukar barang dengan karakter yang ada di depanmu?"
t["3778"] = "Apa itu tentara bayaran bebas?"
t["3779"] = "Apa itu Town Portal Scroll?"
t["3780"] = "Ada apa dengan gens kami? Saya Conrad, Gens Vanert Steward."
t["3781"] = "Pasokan macam apa...?"
t["3782"] = "Level berapa yang harus kucapai?"
t["3783"] = "Apa itu Uniria?"
t["3784"] = "Ketika seorang pejuang yang memilikinya menjadi ahli yang mahir dalam seninya, cincin ini berubah menjadi barang unik yang akan membantu tuannya mencapai ketinggian yang lebih besar."

# 3787-3804
t["3787"] = "Saat pertama kali memasuki Blood Castle, kamu akan menghadapi rintangan: kepercayaan di dalam party-mu. Temui Shadow Phantom Soldier untuk menguji kepercayaan di dalam party-mu, dan hadapi tantangan Blood Castle."
t["3788"] = "Saat kamu memburu monster, kamu bisa menggunakan serangan biasa dengan klik kiri atau menggunakan serangan skill dengan klik kanan."
t["3792"] = "Saat kamu mencapai level tertentu, kamu akan bisa memperbaiki peralatanmu langsung dari jendela Inventory. Level berapakah itu?"
t["3793"] = "Saat ingin membentuk party dengan karakter, hadapi karakter tersebut dan ketik '/party' di jendela Chat. Apa yang harus dilakukan jika tidak ingin menerima permintaan party?"
t["3794"] = "Saat ingin berbicara dengan teman dan kamu tahu ID-nya, atau jika ingin menyampaikan pesan pribadi, kirimkan whisper dengan menggunakan Tab dari jendela Chat."
t["3795"] = "Di mana aku bisa menemukan pedagang?"
t["3796"] = "Di mana aku bisa mempelajari lebih banyak skill?"
t["3797"] = "Di mana aku bisa mempelajari skill?"
t["3798"] = "Tombol mana yang digunakan untuk mengirim whisper dari jendela Chat?"
t["3799"] = "Saat berada di kota, carilah NPC Shadow Phantom Soldier."
t["3800"] = "Whisper digunakan untuk berbicara dengan teman yang jauh atau untuk menyampaikan pesan pribadi. Tombol mana yang digunakan untuk mengirim whisper dari jendela Chat?"
t["3803"] = "Siapa yang perlu kamu temui untuk menggabungkan barang?"
t["3804"] = "Wah! Itu sangat praktis!"

print(f"Part 2 count so far: {len(t)}")

with open('out_data/id/_batch3_partial.json', 'w', encoding='utf-8') as f:
    json.dump(t, f, ensure_ascii=False, indent=1)
print("Saved batch 3 part 2")
