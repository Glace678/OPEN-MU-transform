import json, sys
sys.stdout.reconfigure(encoding='utf-8')

with open('in_data/id/g2.json', 'r', encoding='utf-8') as f:
    data = json.load(f)

strings = data['strings'][519:]  # 172 strings, 520th onwards

# Build translation dictionary
t = {}

# 3625-3658
t["3625"] = "Coba tantangan Chaos Castle Level 2. Kembali padaku setelah mengalahkan 3 Guardsman, dan aku akan memberimu 200.000 Zens sebagai hadiah. Namun, ini hanya berlaku untuk Chaos Castle Level 2."
t["3626"] = "Coba tantangan Chaos Castle Level 2. Kalahkan 3 Guardsman dan kembali temui Shadow Phantom Soldier untuk mengambil hadiahmu. Namun, ini hanya berlaku untuk Chaos Castle Level 2."
t["3627"] = "Coba tantangan Chaos Castle Level 3. Kembali padaku setelah mengalahkan 5 Guardsmen, dan aku akan memberimu 800.000 poin EXP serta 500.000 Zens sebagai hadiah. Namun, ini hanya berlaku untuk Chaos Castle Level 3."
t["3628"] = "Coba tantangan Chaos Castle Level 4. Kembali padaku setelah mengalahkan 1 petualang yang mengenakan Armor of Guardsman, dan aku akan memberimu 300.000 Zens dan 1 Jewel of Chaos sebagai hadiah. Namun, ini hanya berlaku untuk Chaos Castle Level 4."
t["3629"] = "Coba tantangan Chaos Castle Level 4. Kalahkan 1 petualang yang mengenakan Armor of Guardsman dan kembali temui Shadow Phantom Soldier untuk mengambil hadiahmu. Namun, ini hanya berlaku untuk Chaos Castle Level 4."
t["3630"] = "Coba tantangan Chaos Castle Level 5. Kembali padaku setelah mengalahkan 3 petualang yang mengenakan Armor of Guardsman, dan aku akan memberimu 500.000 Zens dan 1 Jewel of Chaos sebagai hadiah. Namun, ini hanya berlaku untuk Chaos Castle Level 5."
t["3631"] = "Coba tantangan Chaos Castle Level 5. Kalahkan 3 petualang yang mengenakan Armor of Guardsman dan kembali temui Shadow Phantom Soldier untuk mengambil hadiahmu. Namun, ini hanya berlaku untuk Chaos Castle Level 5."
t["3632"] = "Coba tantangan Chaos Castle Level 6. Kembali padaku setelah mengalahkan 5 petualang yang mengenakan Armor of Guardsman, dan aku akan memberimu 700.000 Zens dan 1 Jewel of Chaos sebagai hadiah. Namun, ini hanya berlaku untuk Chaos Castle Level 6."
t["3633"] = "Coba tantangan Chaos Castle Level 6. Kalahkan 5 petualang yang mengenakan Armor of Guardsman dan kembali temui Shadow Phantom Soldier untuk mengambil hadiahmu. Namun, ini hanya berlaku untuk Chaos Castle Level 6."
t["3634"] = "Coba tantangan Chaos Castle Level 7. Kembali padaku setelah mengalahkan 10 petualang yang mengenakan Armor of Guardsman, dan aku akan memberimu 1.000.000 Zens dan 2 Jewel of Chaos sebagai hadiah. Namun, ini hanya berlaku untuk Chaos Castle Level 7."
t["3635"] = "Coba tantangan Chaos Castle Level 7. Kalahkan 10 petualang yang mengenakan Armor of Guardsman dan kembali temui Shadow Phantom Soldier untuk mengambil hadiahmu. Namun, ini hanya berlaku untuk Chaos Castle Level 7."
t["3636"] = "Coba tantangan Devil Square Level 1. Kalahkan monster dan raih 50 poin untuk berhasil. Kembalilah padaku setelah selesai untuk mengambil hadiahmu. Namun, ini hanya berlaku untuk Devil Square Level 1."
t["3637"] = "Coba tantangan Devil Square Level 1. Jika kau meraih 50 poin, aku akan memberimu 1 Jewel of Bless sebagai hadiah. Namun, ini hanya berlaku untuk Devil Square Level 1."
t["3638"] = "Coba tantangan Devil Square Level 2. Kalahkan monster dan raih 100 poin untuk berhasil. Kembalilah padaku setelah selesai untuk mengambil hadiahmu. Namun, ini hanya berlaku untuk Devil Square Level 2."
t["3639"] = "Coba tantangan Devil Square Level 3. Kalahkan monster dan raih 130 poin untuk berhasil. Kembalilah padaku setelah selesai untuk mengambil hadiahmu. Namun, ini hanya berlaku untuk Devil Square Level 3."
t["3640"] = "Coba tantangan Devil Square Level 4. Kalahkan monster dan raih 160 poin untuk berhasil. Kembalilah padaku setelah selesai untuk mengambil hadiahmu. Namun, ini hanya berlaku untuk Devil Square Level 4."
t["3641"] = "Coba tantangan Devil Square Level 5. Kalahkan monster dan raih 190 poin untuk berhasil. Kembalilah padaku setelah selesai untuk mengambil hadiahmu. Namun, ini hanya berlaku untuk Devil Square Level 5."
t["3642"] = "Coba tantangan Devil Square Level 6. Kalahkan monster dan raih 210 poin untuk berhasil. Kembalilah padaku setelah selesai untuk mengambil hadiahmu. Namun, ini hanya berlaku untuk Devil Square Level 6."
t["3643"] = "Coba tantangan Devil Square Level 7. Kalahkan monster dan raih 240 poin untuk berhasil. Kembalilah padaku setelah selesai untuk mengambil hadiahmu. Namun, ini hanya berlaku untuk Devil Square Level 7."
t["3644"] = "Coba tantangan Illusion Temple Level 1. Menanglah dan aku akan memberimu 1 Jewel of Soul sebagai hadiah. Namun, ini hanya berlaku untuk Illusion Temple Level 1."
t["3645"] = "Coba tantangan Illusion Temple Level 1. Menangkan pertarungan dan kembali ke Shadow Phantom Soldier untuk mengambil hadiahmu. Namun, ini hanya berlaku untuk Illusion Temple Level 1."
t["3646"] = "Coba tantangan Illusion Temple Level 2. Menanglah dan aku akan memberimu 1 Jewel of Bless sebagai hadiah. Namun, ini hanya berlaku untuk Illusion Temple Level 2."
t["3647"] = "Coba tantangan Illusion Temple Level 2. Menangkan pertarungan dan kembali ke Shadow Phantom Soldier untuk mengambil hadiahmu. Namun, ini hanya berlaku untuk Illusion Temple Level 2."
t["3648"] = "Coba tantangan Illusion Temple Level 3. Menanglah dan aku akan memberimu 1 Jewel of Life sebagai hadiah. Namun, ini hanya berlaku untuk Illusion Temple Level 3."
t["3649"] = "Coba tantangan Illusion Temple Level 3. Menangkan pertarungan dan kembali ke Shadow Phantom Soldier untuk mengambil hadiahmu. Namun, ini hanya berlaku untuk Illusion Temple Level 3."
t["3650"] = "Coba tantangan Illusion Temple Level 4. Menanglah dan aku akan memberimu 1 Jewel of Chaos sebagai hadiah. Namun, ini hanya berlaku untuk Illusion Temple Level 4."
t["3651"] = "Coba tantangan Illusion Temple Level 4. Menangkan pertarungan dan kembali ke Shadow Phantom Soldier untuk mengambil hadiahmu. Namun, ini hanya berlaku untuk Illusion Temple Level 4."
t["3652"] = "Coba tantangan Illusion Temple Level 5. Menanglah dan aku akan memberimu 1 Jewel of Soul sebagai hadiah. Namun, ini hanya berlaku untuk Illusion Temple Level 5."
t["3653"] = "Coba tantangan Illusion Temple Level 5. Menangkan pertarungan dan kembali ke Shadow Phantom Soldier untuk mengambil hadiahmu. Namun, ini hanya berlaku untuk Illusion Temple Level 5."
t["3654"] = "Coba tantangan Illusion Temple Level 6. Menanglah dan aku akan memberimu 1 Jewel of Bless sebagai hadiah. Namun, ini hanya berlaku untuk Illusion Temple Level 6."
t["3655"] = "Coba tantangan Illusion Temple Level 6. Menangkan pertarungan dan kembali ke Shadow Phantom Soldier untuk mengambil hadiahmu. Namun, ini hanya berlaku untuk Illusion Temple Level 6."
t["3656"] = "Cobalah keterampilanmu di Chaos Castle Level 3. Kalahkan 5 Guardsman dan kembali temui Shadow Phantom Soldier untuk mengklaim hadiahmu. Namun, ini hanya berlaku untuk Chaos Castle Level 3."
t["3657"] = "Tutorial itu untuk anak-anak. Tolak saja."
t["3658"] = "Twin Tail di Kanturu Relics sangat merugikan para petualang di sana. Pergilah ke Kanturu Relics dan kalahkan 100 Twin Tail. Temui aku ketika itu selesai. Sebagai hadiah, aku akan memberimu 1 Jewel of Bless. Pada levelmu sekarang, terlalu berbahaya untuk berburu monster sendirian. Akan lebih baik jika berburu dalam party."

# 3669-3674
t["3669"] = "Ketik '/Party off' di jendela Chat."
t["3670"] = "Ketik '/Request off' di jendela Chat."
t["3671"] = "Ketik '/off' di jendela Chat."
t["3672"] = "Tugas yang tidak cocok"
t["3673"] = "Sayangnya, Secrarium bertaruh bahwa kegelapan di hati para petualang akan membangkitkan lebih banyak monster."
t["3674"] = "Uniria adalah barang tunggangan yang bisa kamu gunakan di medan perang. Pasang di slot paling atas dari jendela karakter untuk meningkatkan kecepatan gerakmu."

# 3676
t["3676"] = "Setelah masuk, kau harus menunggu pemain lain masuk. Para pemain akan dikirim ke area tunggu sebelum pertarungan dimulai."

# 3683
t["3683"] = "Menggunakan Town Portal Scroll akan mengembalikanmu ke kota terakhir yang kau kunjungi."

# 3685
t["3685"] = "Namun, dengan tombol M, dimungkinkan untuk bepergian ke tempat yang jauh dalam sekejap."

# 3692
t["3692"] = "Vanert Reicht mungkin telah menyerahkan kepemimpinan Aliansi Bangsawan Arca kepada Duprian Winstern, tapi itu tidak berarti kita bisa bersikap santai."

# 3706-3714
t["3706"] = "Baiklah. Berikan misinya padaku."
t["3707"] = "Baiklah. Berapa banyak yang harus kubeli?"
t["3708"] = "Baiklah. Aku akan menerima tantangan ini."
t["3709"] = "Baiklah. Sepertinya Gorgon cukup merepotkan bagi petualang pemula di Dungeon. Aku akan memberimu tugas untuk mengalahkan 20 Gorgon. Bagaimana menurutmu?"
t["3710"] = "Baiklah. Sepertinya Hell Hound cukup merepotkan bagi petualang pemula di Dungeon. Aku akan memberimu tugas untuk mengalahkan 20 Hell Hound. Bagaimana menurutmu?"
t["3711"] = "Baiklah. Sepertinya Hell Spider cukup merepotkan bagi petualang pemula di Dungeon. Aku akan memberimu tugas untuk mengalahkan 20 Hell Spider. Bagaimana menurutmu?"
t["3712"] = "Baiklah. Sepertinya Larva cukup merepotkan bagi petualang pemula di Dungeon. Aku akan memberimu tugas untuk mengalahkan 20 Larva. Bagaimana menurutmu?"
t["3713"] = "Baiklah. Izinkan aku mencobanya. (Terima)"
t["3714"] = "Baiklah. Ceritakan lebih lanjut."

# 3728
t["3728"] = "Kunjungi Shadow Phantom Soldier untuk menerima hadiah kecil."

# 3741
t["3741"] = "Prajurit! Selamat datang."

# 3744-3746
t["3744"] = "Penjaga Kundun (1)"
t["3745"] = "Penjaga Kundun (2)"
t["3746"] = "Penjaga Kundun (3)"

# 3749-3751
t["3749"] = "Kita punya banyak hal yang harus dibahas. Mari lanjut ke langkah berikutnya."
t["3750"] = "Kami meluncurkan penyelidikan setelah komunikasi dengan tim pengintai kami di Dungeon mulai jarang dan kami menemukan bahwa mereka diserang oleh monster. Kami mengirim pasukan bantuan tapi sayangnya mereka terlambat... Misi pertama unit Shadow Phantom adalah mencari dan menghukum monster yang menyerang tim pengintai kami. Maukah kau menerima misi ini?"
t["3751"] = "Kita sudah hampir di akhir tutorial. Sekarang setelah kau punya keterampilan, berburu monster akan menjadi lebih mudah. Mari kita coba keterampilanmu."

# 3761-3769
t["3761"] = "Selamat datang kembali. Sekarang yang perlu kau lakukan hanyalah melenyapkan Gaion di Varka. Apa kau siap?"
t["3762"] = "Selamat datang di MU.;Aku akan membantumu di jalanmu menjadi prajurit sejati."
t["3763"] = "Selamat datang di cabang Devias serikat pedagang yang baru didirikan. Kami adalah orang yang tepat untuk berbagai permintaan di benua ini."
t["3764"] = "Selamat datang di dunia Mu.;Kami telah menunggu pahlawan sepertimu untuk tiba dan menghentikan kekacauan yang dibawa Kundun ke alam Mu."
t["3765"] = "Selamat datang, ini serikat pedagang. Kami menyampaikan berbagai permintaan kepada pedagang yang terdaftar."
t["3766"] = "Bagus. Sekarang tolong bawa tongkat Burnt Murderer dari Vulcanus kepadaku. (Lv. 350 - 400)"
t["3767"] = "Yah, kau mungkin tidak ingin tahu lebih banyak tentang dia, tapi misi tetaplah misi."
t["3768"] = "Apakah kau bisa mendapatkan banyak barang untuk latihanmu? Aku harus memberitahumu bahwa tidak."
t["3769"] = "Apakah kau bisa mendapatkan banyak barang untuk latihanmu? Aku harus memberitahumu bahwa tidak."

# 3773-3784
t["3773"] = "Apa yang dilakukan penjaga ratu di tempat seperti ini?"
t["3774"] = "Apa yang bisa kubantu? Ah, kau datang untuk permintaan dari kami. Apa kau tahu Gaion? Dia sebenarnya adalah saudara seperjuangan dari ketua serikat pedagang pertama tapi para tetua ingin menyingkirkannya karena dia dianggap simbol pengkhianatan. Aku tidak mengerti kekhawatiran mereka karena dia tidak akan pernah bisa meninggalkan Varka. (Lv. 350 - 400)"
t["3775"] = "Apa yang bisa kubantu? Ah, kau orang dari serikat pedagang! Sejujurnya, aku tidak akan pernah meminta bantuan serikat pedagang jika aku punya pilihan lain. Yah, sudahlah. Ada 2 permintaan, pertama mengumpulkan bubuk Stardust dan kedua mendapatkan permata langka. Maukah kau menerimanya? (Lv. 350 - 400)"
t["3776"] = "Apa yang harus dilakukan jika tidak ingin menerima permintaan party?"
t["3777"] = "Apa yang harus dilakukan jika ingin berdagang dengan karakter yang ada di depanmu?"
t["3778"] = "Apa itu pedagang bebas?"
t["3779"] = "Apa itu Town Portal Scroll ini?"
t["3780"] = "Apa keperluanmu dengan gens kami? Aku Conrad, Gens Vanert Steward."
t["3781"] = "Persediaan macam apa...?"
t["3782"] = "Level berapa yang harus kucapai?"
t["3783"] = "Apa itu Uniria?"
t["3784"] = "Ketika seorang prajurit yang memilikinya menjadi ahli sejati dalam seninya, cincin ini berubah menjadi barang unik yang akan membantu tuannya mencapai ketinggian yang lebih besar."

# 3787-3788
t["3787"] = "Saat pertama kali memasuki Blood Castle, kau akan menemui kendala: kepercayaan di dalam."
t["3788"] = "Saat berburu monster, kau bisa menggunakan serangan biasa dengan klik kiri atau menggunakan keterampilan dengan klik kanan."

# 3792-3800
t["3792"] = "Ketika mencapai level tertentu, kau akan bisa memperbaiki peralatanmu secara langsung."
t["3793"] = "Saat ingin membentuk party dengan suatu karakter, hadapi karakter itu dan masukkan '/party' di jendela chat."
t["3794"] = "Saat ingin berbicara dengan teman dan kau tahu ID-nya, atau jika ingin mengirim pesan pribadi..."
t["3795"] = "Di mana aku bisa menemukan pedagang?"
t["3796"] = "Di mana aku bisa mempelajari lebih banyak keterampilan?"
t["3797"] = "Di mana aku bisa mempelajari keterampilan?"
t["3798"] = "Tombol mana yang digunakan untuk mengirim bisikan dari jendela Chat?"
t["3799"] = "Saat berada di kota, carilah NPC Shadow Phantom Soldier."
t["3800"] = "Bisikan digunakan untuk berbicara dengan teman yang jauh atau menyampaikan pesan pribadi."

# 3803-3804
t["3803"] = "Siapa yang harus kau temui untuk menggabungkan barang?"
t["3804"] = "Wah! Benar-benar praktis!"

# 3834
t["3834"] = "Lenyapkan Iron Rider yang Sulit Ditemukan"

# 3836
t["3836"] = "Dengan bantuan NPC di kota, kau tidak hanya bisa menggabungkan barang, tapi juga memperkuat dan memperbaikinya."

# 3856-3858
t["3856"] = "Maukah kau membawakan Zirah Tantalos terlebih dahulu? Aku akan memberimu hadiah yang pantas ketika kau menyelesaikannya. (Lv. 350 - 400)"
t["3857"] = "Maukah kau mengunjungi Chaos Castle, arena para dewa? Tidak ada tempat yang lebih baik untuk menguji batas kemampuanmu."
t["3858"] = "Ya, aku akan menerimanya. (Terima)"

# 3859-3863
t["3859"] = "Ya, tolong. (Terima)"
t["3860"] = "Ya, segera. (Terima)"
t["3861"] = "Ya. Aku ingin bergabung."
t["3862"] = "Ya. Aku ingin keluar."
t["3863"] = "Ya. Aku akan mengikuti tesmu."

# 3865-3872
t["3865"] = "Kamu adalah anggota gens yang berbeda."
t["3866"] = "Kau kembali lebih cepat dari yang kuduga. Aku punya misi yang akan memberimu barang yang kau butuhkan di Blood Castle."
t["3867"] = "Kau kembali lebih cepat dari yang kuduga. Aku punya misi sederhana yang akan memberimu Scroll of Blood."
t["3868"] = "Kau kembali lebih cepat dari yang kuduga. Aku punya misi sederhana yang akan memberimu Scroll of Blood."
t["3869"] = "Kau kembali lebih cepat dari yang kuduga. Aku punya misi sederhana yang akan memberimu Invisibility Cloak."
t["3870"] = "Kau kembali lebih cepat dari yang kuduga. Aku punya misi sederhana yang akan memberimu Invisibility Cloak."
t["3871"] = "Kau kembali lebih cepat dari yang kuduga. Aku akan memberimu misi yang akan memberimu Armor of Guardsman."
t["3872"] = "Kau kembali lebih cepat dari yang kuduga. Aku akan memberimu misi yang akan memberimu Armor of Guardsman."

# 3874
t["3874"] = "Kau penuh semangat! Baiklah. Mari kita coba menambahkan barang ke tasmu. Bawa barang ini ke tasmu."

# 3876-3877
t["3876"] = "Kau bisa mendapatkan peralatan yang sangat bagus di Illusion Temple. Bersiaplah dengan matang sebelum masuk."
t["3877"] = "Kau bisa menemukan kekayaan dan ketenaran di Illusion Temple. Jika kau mengikuti tantangan kuil, banyak hal menantimu."

# 3879
t["3879"] = "Kau bisa memperbaiki peralatanmu oleh Pandai Besi Hanzo (117,140) di Lorencia."

# 3884-3890
t["3884"] = "Kau bisa mendaftar ke serikat pedagang sebagai pedagang bebas dengan 1 juta Zen."
t["3885"] = "Kau tidak bisa bergabung dengan gens jika kau anggota aliansi guild."
t["3886"] = "Kau tidak bisa bergabung dengan gens jika kau anggota aliansi guild. Tinggalkan aliansi guild terlebih dahulu."
t["3887"] = "Kau tidak bisa bergabung dengan gens saat berada di dalam party."
t["3888"] = "Kau tidak bisa bergabung dengan gens tersebut."
t["3889"] = "Kau tidak bisa keluar dari gens tersebut."
t["3890"] = "Kau tidak memiliki cukup ruang untuk barang tersebut. Kosongkan tasmu dan coba lagi."

# 3895-3908
t["3895"] = "Kau telah menerima ujian pertama. Aku ingin kau membuktikan kemampuan bertempurmu dengan mengalahkan monster di Lorencia."
t["3896"] = "Kau sudah bergabung dengan gens tersebut."
t["3897"] = "Kau telah menyelesaikan ujian kelima menjadi pahlawan. Misi berikutnya yang akan kuberikan adalah..."
t["3898"] = "Kau telah menyelesaikan ujian pertama menjadi pahlawan. Misi berikutnya yang akan kuberikan adalah menguji apakah kau punya keberanian menjadi pahlawan. Maukah kau menerima misi ini?"
t["3899"] = "Kau telah menyelesaikan tutorial. Ambil Cincin Pahlawan dan Cincin Prajurit ini."
t["3900"] = "Kau telah keluar dari gens."
t["3901"] = "Kau belum bergabung dengan gens."
t["3902"] = "Kau belum bergabung dengan gens. Kau harus menjadi anggota gens terlebih dahulu untuk menerima misi ini."
t["3903"] = "Kau kini telah menyelesaikan ujian keempat menjadi pahlawan. Misi berikutnya yang akan kuberikan adalah..."
t["3904"] = "Kau kini telah menyelesaikan ujian kedua menjadi pahlawan. Misi berikutnya yang akan kuberikan adalah..."
t["3905"] = "Kau kini telah menyelesaikan ujian ketiga menjadi pahlawan. Misi berikutnya yang akan kuberikan adalah..."
t["3906"] = "Kau harus setidaknya level 50 untuk bergabung dengan gens."
t["3907"] = "Kau sudah tahu cukup banyak. Aku tidak ingin membicarakannya. (Tolak)"
t["3908"] = "Kau telah keluar dari gens. Kontribusimu telah direset ke 0."

# 3910
t["3910"] = "Kau mungkin tidak akan menganggap Blood Castle menantang lagi. Namun, itu tetap akan menjadi pengalaman yang berharga."

# 3913-3914
t["3913"] = "Kau akan menunggu sekitar satu menit sebelum benar-benar masuk. Acara dimulai ketika waktu tunggu habis."
t["3914"] = "Kau akan menunggu sekitar satu menit sebelum benar-benar masuk. Acara dimulai ketika waktu tunggu habis."

# 3915
t["3915"] = "Kau akan menunggu sekitar satu menit sebelum benar-benar masuk. Kau akan masuk ke Blood Castle setelah waktu tunggu selesai."

# 3916
t["3916"] = "Kau akan salah jika mengira sudah mencapai puncak. Menguji dirimu dan kemampuanmu adalah cara terbaik untuk tumbuh lebih kuat."

# 3917
t["3917"] = "Kau akan bisa menggunakan Cincin Pahlawan di level 40 dan Cincin Prajurit di level 80. Menjatuhkan cincin akan memecahkan segelnya dan mengubahnya menjadi barang.;Lain kali kita bertemu, aku sungguh berharap melihatmu menjadi prajurit yang lebih kuat. Pergilah sekarang dan bangun pengalaman serta kekuatanmu dan bebaskan dunia ini dari cengkeraman Kundun!;(Kau telah menyelesaikan tutorial.)"

# 3918-3924
t["3918"] = "Kau akan menghadapi monster kuat yang datang terus-menerus di Devil Square. Membentuk party akan membuat perburuan lebih mudah."
t["3919"] = "Kau perlu memperbaiki peralatanmu secara teratur karena daya tahannya akan berkurang seiring penggunaan."
t["3920"] = "Kau tidak akan bisa menggunakan keterampilan tertentu seperti Teleport atau Telekinesis saat memegang Uniria."
t["3921"] = "Kau kini akan semakin sulit menemukan tempat berburu yang cocok. Bagaimana kalau mencoba tempat yang lebih menantang?"
t["3922"] = "Kau sekarang harus lebih fokus pada serangan terkoordinasi dengan rekan-rekanmu daripada hanya mengandalkan kekuatan sendiri."
t["3923"] = "Kau akan menunggu sekitar satu menit sebelum benar-benar masuk. Setelah waktu tunggu selesai, kau akan dikirim ke area pertarungan."
t["3924"] = "Kau kembali lebih cepat dari yang kuduga. Aku punya misi yang akan memberimu barang yang kau butuhkan."

# 3925-3928
t["3925"] = "Kau berpengetahuan luas. Aku akan memberimu barang yang kau butuhkan untuk mengakses Chaos Castle."
t["3926"] = "Kau berpengetahuan luas. Aku akan memberimu barang yang kau butuhkan untuk memasuki Devil Square."
t["3927"] = "Kau berpengetahuan luas. Aku bertanggung jawab membagikan barang yang diperlukan untuk acara pertarungan."
t["3928"] = "Kau berpengetahuan luas. Untuk menembus penghalang di sekitar Blood Castle, kau membutuhkan Invisibility Cloak."

# 3929
t["3929"] = "Kau telah melakukannya dengan baik. Mari kita selesaikan satu hal lagi sebelum aku memintamu menghadapi monster yang lebih tangguh."

# 3930-3934
t["3930"] = "Kau telah melakukannya dengan baik. Keterampilan Falling Slash sekarang seharusnya menjadi salah satu keterampilan yang bisa kau gunakan. Klik ikonnya.;Apakah ikon keterampilan di sebelah kanan berubah menjadi Falling Slash? Saat berburu monster, cukup klik kanan mouse untuk menggunakannya."
t["3931"] = "Kau telah melakukannya dengan baik. Keterampilan Fire Ball sekarang seharusnya menjadi salah satu keterampilan yang bisa kau gunakan. Klik ikonnya.;Apakah ikon keterampilan di sebelah kanan berubah menjadi Fire Ball? Saat berburu monster, cukup klik kanan mouse untuk menggunakannya."
t["3932"] = "Kau telah melakukannya dengan baik. Keterampilan Summon Goblin sekarang seharusnya menjadi salah satu keterampilan yang bisa kau gunakan. Klik ikonnya.;Apakah ikon keterampilan di sebelah kanan berubah menjadi Summon Goblin? Saat berburu monster, cukup klik kanan mouse untuk menggunakannya."
t["3933"] = "Kau telah melakukannya dengan baik. Keterampilan Triple Shot sekarang seharusnya menjadi salah satu keterampilan yang bisa kau gunakan. Klik ikonnya.;Apakah ikon keterampilan di sebelah kanan berubah menjadi Triple Shot? Saat berburu monster, cukup klik kanan mouse untuk menggunakannya."
t["3934"] = "Kau sudah cukup menjelaskan. Mari lanjut tutorialnya."

# 3935-3938
t["3935"] = "Kau telah membuat pilihan yang bijak. Misi pertama untuk ujian ketiga melibatkan mengalahkan 20 Guardsman di Chaos Castle, mengantarkan senjata Archangel di Blood Castle, dan mengalahkan monster di Devil Square. Maukah kau menerima tantangan ini?"
t["3936"] = "Kau telah mencapai tahap akhir Blood Castle, tapi itu seharusnya tetap menyenangkan."
t["3937"] = "Kau benar-benar telah menjadi prajurit yang tangguh. Aku yakin ratu sendiri akan bangga padamu."
t["3938"] = "Guildmu adalah bagian dari gens yang berbeda."

# 3940
t["3940"] = "Zaikan dari Tarkan 2 seharusnya cukup. Pergilah ke Tarkan 2 dan kalahkan 30 Zaikan. Temui aku ketika itu selesai."

# 3963-3968 (garbled chars, treat as ellipsis dots)
t["3963"] = ".... Dia tampak seperti pelit...."
t["3964"] = ".... Hmm, untuk klan...."
t["3965"] = ".... Sesuatu yang mengganggu tampaknya terjadi...."
t["3966"] = ".... Itulah yang terjadi."
t["3967"] = "....Keduanya cukup mudah..."
t["3968"] = "......Apa yang ada di balik cerita ini?"

# Verify
expected = [str(s['id']) for s in strings]
translated = set(t.keys())
missing = [sid for sid in expected if sid not in translated]
extra = [sid for sid in translated if sid not in expected]

print(f'Expected: {len(expected)}')
print(f'Translated: {len(t)}')
print(f'Missing: {len(missing)} -> {missing[:20]}')
print(f'Extra: {len(extra)}')

# Semicolon check
mismatches = []
for s in strings:
    sid = str(s['id'])
    if sid in t:
        o = s['en'].count(';')
        tr = t[sid].count(';')
        if o != tr:
            mismatches.append((sid, o, tr))

print(f'Semicolon mismatches: {len(mismatches)}')
for sid, o, tr in mismatches:
    print(f'  id={sid}: orig={o}, trans={tr}')

# Save
with open('out_data/id/_batch4_partial.json', 'w', encoding='utf-8') as f:
    json.dump(t, f, ensure_ascii=False)

print(f'\nSaved {len(t)} translations to _batch4_partial.json')
