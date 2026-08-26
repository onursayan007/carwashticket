using CarWashTicket.Api.Entities;

namespace CarWashTicket.Api.Data;

// Yıkama noktasının ne sattığını belirler; StationType ve hizmet listesi buradan türer.
public enum SeedFlavor
{
    // Jetonlu self servis peron: su, köpük, fırça birim birim alınır.
    Jetonlu,
    // Temassız robot veya tünel: hazır programlar.
    Robotik,
    // Oto yıkamacı: aracı teslim alır, paket satar.
    ElleYikama,
    // Hem peron hem paket.
    Karma
}

public record StationSeed(
    string Name,
    SeedFlavor Flavor,
    string City,
    string District,
    string Address,
    double Latitude,
    double Longitude,
    decimal Rating,
    string? Phone);

// Antalya'da gerçek konumlardan derlenmiş demo verisi.
// Puanlar ve konumlar gerçeğe yakın, fiyatlar ve hizmet listeleri uydurmadır.
public static class SeedStations
{
    public static readonly StationSeed[] All =
    [
        // --- Elmalı ---
        new("Adil Oto Yıkama - Frog Wash", SeedFlavor.ElleYikama, "Antalya", "Elmalı", "Yeni Mah. Finike Cad.", 36.734204, 29.921421, 3.7m, "+90 242 618 10 21"),
        new("Yasinim Buharlı Oto Yıkama", SeedFlavor.ElleYikama, "Antalya", "Elmalı", "Gündoğan Mah. Hükümet Cad. No:84", 36.735059, 29.921394, 4.1m, "+90 242 618 22 40"),
        new("Özgür Oto Kuaförü", SeedFlavor.ElleYikama, "Antalya", "Elmalı", "Yeni Mah. Terminal Cad.", 36.731958, 29.916951, 5.0m, "+90 242 618 33 07"),
        new("Elmalı Oto Kuaför", SeedFlavor.ElleYikama, "Antalya", "Elmalı", "Yeni Mah. Antalya Cad.", 36.732898, 29.927080, 3.4m, null),
        new("Diamond Car Wash", SeedFlavor.ElleYikama, "Antalya", "Elmalı", "Yeni Mah. Atatürk Blv.", 36.730058, 29.931271, 5.0m, "+90 242 618 55 90"),
        new("Çetinkaya Oto Dizayn", SeedFlavor.ElleYikama, "Antalya", "Elmalı", "Karyağdı Mah. Bahçelievler Cad. No:10", 36.731811, 29.912707, 4.4m, "+90 536 520 70 17"),
        new("Kardeşler Oto Yıkama", SeedFlavor.ElleYikama, "Antalya", "Elmalı", "Karyağdı Mah. Ünal Özgödek Cad. No:64", 36.738857, 29.914607, 1.0m, null),
        new("Şenol Oto Yıkama", SeedFlavor.ElleYikama, "Antalya", "Elmalı", "Karyağdı Mah. Bahçeli Cami Sk.", 36.739729, 29.914874, 3.9m, null),
        new("Ayaz Auto Detailing", SeedFlavor.ElleYikama, "Antalya", "Elmalı", "Elmalı Sanayi Sitesi", 36.706095, 29.914341, 5.0m, "+90 535 057 07 94"),
        new("Ümit Oto Yıkama", SeedFlavor.ElleYikama, "Antalya", "Elmalı", "Elmalı Sanayi Sitesi", 36.706030, 29.913208, 4.0m, null),
        new("Kışla Oto Yıkama", SeedFlavor.ElleYikama, "Antalya", "Elmalı", "Kışla Mah. Elmalı-Seki Yolu", 36.740064, 29.855716, 5.0m, null),
        new("Petrol Ofisi Karyağdı", SeedFlavor.Jetonlu, "Antalya", "Elmalı", "Karyağdı Mah. Ünal Özgödek Cad. No:134", 36.728594, 29.899297, 3.9m, "+90 800 211 0229"),
        new("Lukoil Elmalı", SeedFlavor.Jetonlu, "Antalya", "Elmalı", "Yeni Mah. M. Hamdi Yazır Blv. No:104", 36.731571, 29.937518, 3.6m, "+90 444 4585"),
        new("Shell Düden", SeedFlavor.Jetonlu, "Antalya", "Elmalı", "Düden Mah. Elmalı-Finike Yolu", 36.699300, 29.917000, 4.8m, "+90 532 646 00 16"),
        new("Opet Elmalı", SeedFlavor.Karma, "Antalya", "Elmalı", "Yeni Mah. Finike Cad. No:287", 36.715876, 29.920406, 4.3m, "+90 444 6738"),
        new("Petrol Ofisi Akçay", SeedFlavor.Jetonlu, "Antalya", "Elmalı", "Akçay Mah. Elmalı-Akçay Yolu", 36.606948, 29.757727, 4.2m, "+90 850 227 2613"),
        new("Kepezler Petrol - Aytemiz", SeedFlavor.Karma, "Antalya", "Elmalı", "Çobanisa Mah. Çobanisa Sk. No:5/1", 36.858357, 30.027176, 5.0m, "+90 532 516 78 87"),

        // --- Kepez ---
        new("A&R Detailing", SeedFlavor.ElleYikama, "Antalya", "Kepez", "Yeni Mah.", 36.920420, 30.714357, 5.0m, "+90 552 683 07 07"),
        new("AS Oto Yıkama", SeedFlavor.ElleYikama, "Antalya", "Kepez", "Ünsal Mah.", 36.928557, 30.633503, 5.0m, "+90 533 819 28 59"),
        new("Altınay Oto Yıkama Kuaför Detailing", SeedFlavor.ElleYikama, "Antalya", "Kepez", "Kanal Mah.", 36.931929, 30.662593, 5.0m, "+90 539 649 25 26"),
        new("Alya Oto Kuaförü", SeedFlavor.ElleYikama, "Antalya", "Kepez", "Erenköy Mah.", 36.926615, 30.671093, 4.9m, "+90 553 962 74 71"),
        new("Arslan Garage", SeedFlavor.ElleYikama, "Antalya", "Kepez", "Yeşilyurt Mah.", 36.910002, 30.643727, 5.0m, "+90 553 498 32 48"),
        new("Aslan Oto Yıkama & Detailing", SeedFlavor.ElleYikama, "Antalya", "Kepez", "Şafak Mah. (Yeni Sanayi)", 36.923253, 30.637257, 4.9m, "+90 530 843 72 85"),
        new("Asya Oto Yıkama ve Detailing", SeedFlavor.ElleYikama, "Antalya", "Kepez", "Gündoğdu Mah.", 36.926682, 30.701869, 5.0m, "+90 541 464 52 85"),
        new("B-EMR Car Wash & Detailing", SeedFlavor.ElleYikama, "Antalya", "Kepez", "Hüsnü Karakaş Mah.", 36.934130, 30.728202, 5.0m, "+90 534 973 69 70"),
        new("Best Oto Yıkama", SeedFlavor.ElleYikama, "Antalya", "Kepez", "Avni Tolunay Mah.", 36.906634, 30.634183, 5.0m, "+90 539 740 06 20"),
        new("Bycar Detailing Oto Yıkama", SeedFlavor.ElleYikama, "Antalya", "Kepez", "Kültür Mah.", 36.907530, 30.652697, 4.9m, "+90 530 967 61 07"),
        new("Car Life Wash Oto Yıkama", SeedFlavor.ElleYikama, "Antalya", "Kepez", "Yenidoğan Mah.", 36.908455, 30.660961, 4.6m, "+90 544 248 19 20"),
        new("CarVita Oto Yıkama", SeedFlavor.ElleYikama, "Antalya", "Kepez", "Yeni Emek Mah.", 36.925280, 30.695174, 4.6m, "+90 542 150 19 85"),
        new("DMR Oto Yıkama & Detailing", SeedFlavor.ElleYikama, "Antalya", "Kepez", "Yeni Emek Mah.", 36.923911, 30.701268, 5.0m, "+90 539 716 13 70"),
        new("Fabrika Oto Yıkama", SeedFlavor.ElleYikama, "Antalya", "Kepez", "Güneş Mah.", 36.924213, 30.736609, 4.0m, null),
        new("Hocaoğlu Oto Yıkama Yağlama", SeedFlavor.ElleYikama, "Antalya", "Kepez", "Emek Mah.", 36.913058, 30.706488, 3.9m, "+90 531 287 05 96"),
        new("K&T Car Detailing Oto Yıkama", SeedFlavor.ElleYikama, "Antalya", "Kepez", "Sütçüler Mah.", 36.928822, 30.714827, 4.7m, null),
        new("Karsu Oto Yıkama", SeedFlavor.ElleYikama, "Antalya", "Kepez", "Erenköy Mah.", 36.932023, 30.669628, 5.0m, "+90 543 193 07 99"),
        new("Parlax Oto Yıkama", SeedFlavor.ElleYikama, "Antalya", "Kepez", "Teomanpaşa Mah.", 36.921244, 30.722948, 4.7m, "+90 542 290 76 67"),
        new("Pearl Automotive Antalya", SeedFlavor.ElleYikama, "Antalya", "Kepez", "Gülveren Mah.", 36.903308, 30.644871, 4.7m, "+90 541 717 70 07"),
        new("Profesyonel Oto Yıkama", SeedFlavor.ElleYikama, "Antalya", "Kepez", "Avni Tolunay Mah.", 36.900938, 30.640714, 4.1m, null),
        new("SC Auto Detailing Oto Yıkama", SeedFlavor.ElleYikama, "Antalya", "Kepez", "Sütçüler Mah.", 36.933363, 30.715022, 5.0m, "+90 507 503 84 70"),
        new("Trend Oto Yıkama - Detailing", SeedFlavor.ElleYikama, "Antalya", "Kepez", "Kuzeyyaka Mah.", 36.941830, 30.706108, 4.9m, "+90 535 213 49 86"),
        new("Çalışkan Premium Car Wash", SeedFlavor.ElleYikama, "Antalya", "Kepez", "Sütçüler Mah. Hastane Cd.", 36.931450, 30.721739, 4.6m, "+90 537 046 15 73"),
        new("Özgüven Oto Yıkama Kepez", SeedFlavor.ElleYikama, "Antalya", "Kepez", "Karşıyaka Mah.", 36.913830, 30.696416, 4.1m, null),
        new("Tornado Robotik Yıkama (Altınova)", SeedFlavor.Robotik, "Antalya", "Kepez", "Altınova Cd. 92/1", 36.931164, 30.772593, 4.0m, "+90 541 137 88 87"),
        new("Tornado Robotik Yıkama (Dumlupınar)", SeedFlavor.Robotik, "Antalya", "Kepez", "Yenidoğan, Dumlupınar Blv. 4/1", 36.919013, 30.666558, 1.5m, "+90 541 137 88 87"),
        new("Tornado Robotik Yıkama (Fevzi Çakmak)", SeedFlavor.Robotik, "Antalya", "Kepez", "Fevzi Çakmak, 6228. Sk. 19", 36.948992, 30.702842, 3.9m, "+90 541 137 88 87"),
        new("Petrol Ofisi (Fevzi Çakmak)", SeedFlavor.Karma, "Antalya", "Kepez", "Fevzi Çakmak, 6228. Sk. 19", 36.948842, 30.706767, 4.2m, "+90 850 220 9725"),
        new("Opet (Sakarya Blv.)", SeedFlavor.Jetonlu, "Antalya", "Kepez", "Karşıyaka, Sakarya Blv. 268", 36.918400, 30.704600, 3.7m, "+90 444 6738"),
        new("Petrol Ofisi (Sakarya Blv.)", SeedFlavor.Jetonlu, "Antalya", "Kepez", "Yeni Emek, Sakarya Blv. 221", 36.918870, 30.697838, 4.1m, "+90 800 211 0229"),
        new("Shell (Sakarya Blv.)", SeedFlavor.Jetonlu, "Antalya", "Kepez", "Kanal, Sakarya Blv. 34", 36.921256, 30.674415, 4.0m, "+90 242 345 30 40"),
        new("Shell Oil Sezpet", SeedFlavor.Jetonlu, "Antalya", "Kepez", "Kültür, 75. Yıl Cd. 60", 36.907116, 30.649698, 4.0m, "+90 242 226 13 10"),

        // --- Muratpaşa ---
        new("07 Podium Oto Yıkama", SeedFlavor.ElleYikama, "Antalya", "Muratpaşa", "Kışla Mah.", 36.888431, 30.700975, 5.0m, "+90 553 667 71 07"),
        new("AGN Oto Yıkama & Kuaför", SeedFlavor.ElleYikama, "Antalya", "Muratpaşa", "Meltem Mah.", 36.888122, 30.675290, 5.0m, "+90 242 503 19 03"),
        new("Ada Oto Yıkama Çallı", SeedFlavor.ElleYikama, "Antalya", "Muratpaşa", "Güvenlik Mah.", 36.903829, 30.682103, 5.0m, "+90 501 480 60 07"),
        new("Aral Car Wash Lara", SeedFlavor.ElleYikama, "Antalya", "Muratpaşa", "Güzeloluk Mah.", 36.862821, 30.772310, 4.9m, "+90 539 673 61 31"),
        new("Arya Auto Spa", SeedFlavor.ElleYikama, "Antalya", "Muratpaşa", "Güzeloluk Mah.", 36.863801, 30.776029, 5.0m, "+90 544 464 34 30"),
        new("Asel Oto Yıkama Detailing", SeedFlavor.ElleYikama, "Antalya", "Muratpaşa", "Şirinyalı Mah.", 36.862761, 30.734859, 4.0m, "+90 507 641 75 95"),
        new("BLT Auto Oto Yıkama", SeedFlavor.ElleYikama, "Antalya", "Muratpaşa", "Kızılsaray Mah.", 36.891822, 30.694933, 5.0m, "+90 540 030 32 32"),
        new("Brothers Galeri Oto Yıkama Detailing", SeedFlavor.ElleYikama, "Antalya", "Muratpaşa", "Tahılpazarı Mah.", 36.892332, 30.707814, 5.0m, "+90 539 445 65 07"),
        new("Caryonautocare", SeedFlavor.ElleYikama, "Antalya", "Muratpaşa", "Fener Mah.", 36.850566, 30.761559, 5.0m, "+90 547 168 58 58"),
        new("Cem Detailing Oto Güzellik Merkezi", SeedFlavor.ElleYikama, "Antalya", "Muratpaşa", "Zerdalilik Mah.", 36.883203, 30.716108, 4.6m, "+90 505 099 60 67"),
        new("Easy Car Wash", SeedFlavor.ElleYikama, "Antalya", "Muratpaşa", "Yeşilbahçe Mah.", 36.867777, 30.728173, 4.9m, "+90 541 231 72 08"),
        new("Elit Garage Detailing", SeedFlavor.ElleYikama, "Antalya", "Muratpaşa", "Demircikara Mah.", 36.876514, 30.717749, 4.8m, "+90 554 697 60 62"),
        new("GZL Oto Yıkama Car Wash", SeedFlavor.ElleYikama, "Antalya", "Muratpaşa", "Zerdalilik Mah.", 36.882361, 30.717539, 4.9m, "+90 545 445 24 35"),
        new("Gold Car Wash", SeedFlavor.ElleYikama, "Antalya", "Muratpaşa", "Güzeloba Mah.", 36.849133, 30.803187, 4.9m, "+90 533 150 15 98"),
        new("Işıltı Oto Yıkama", SeedFlavor.ElleYikama, "Antalya", "Muratpaşa", "Yeşilbahçe Mah.", 36.865410, 30.728127, 4.4m, "+90 541 774 07 05"),
        new("Karya Oto Yıkama Park", SeedFlavor.ElleYikama, "Antalya", "Muratpaşa", "Gençlik Mah.", 36.878220, 30.711884, 4.7m, "+90 507 643 35 32"),
        new("Kundu Oto Yıkama", SeedFlavor.ElleYikama, "Antalya", "Muratpaşa", "Güzeloba Mah.", 36.860899, 30.831896, 3.6m, "+90 532 648 94 13"),
        new("Night Garaj Oto Yıkama", SeedFlavor.ElleYikama, "Antalya", "Muratpaşa", "Çağlayan Mah.", 36.863386, 30.771282, 5.0m, "+90 532 508 07 10"),
        new("Okan Oto Yıkama & Detailing", SeedFlavor.ElleYikama, "Antalya", "Muratpaşa", "Gebizli Mah.", 36.898811, 30.726186, 4.6m, "+90 545 268 35 97"),
        new("Oto Yıkama Kristal", SeedFlavor.ElleYikama, "Antalya", "Muratpaşa", "Varlık Mah.", 36.889952, 30.684363, 5.0m, "+90 541 870 65 02"),
        new("Pascha Otomotiv", SeedFlavor.ElleYikama, "Antalya", "Muratpaşa", "Çağlayan Mah.", 36.854833, 30.780832, 4.4m, "+90 536 556 90 90"),
        new("Prime Garage", SeedFlavor.ElleYikama, "Antalya", "Muratpaşa", "Yıldız Mah.", 36.894209, 30.683950, 5.0m, "+90 538 694 24 06"),
        new("Pro Car Care Oto Detay", SeedFlavor.ElleYikama, "Antalya", "Muratpaşa", "Cumhuriyet Mah. (Eski Sanayi)", 36.903701, 30.694589, 4.5m, "+90 537 666 00 21"),
        new("Puris Lara Profesyonel Oto Yıkama", SeedFlavor.ElleYikama, "Antalya", "Muratpaşa", "Şirinyalı Mah.", 36.863829, 30.729452, 3.8m, "+90 242 316 86 85"),
        new("Seta Oto Yıkama ve Detaylı Temizlik", SeedFlavor.ElleYikama, "Antalya", "Muratpaşa", "Zümrütova Mah.", 36.867034, 30.738302, 5.0m, "+90 541 364 75 37"),
        new("Yumoş Oto Yıkama", SeedFlavor.ElleYikama, "Antalya", "Muratpaşa", "Balbey Mah.", 36.890871, 30.709984, 5.0m, "+90 507 289 14 05"),
        new("İska Car Detailing", SeedFlavor.ElleYikama, "Antalya", "Muratpaşa", "Şirinyalı Mah.", 36.863527, 30.734690, 4.3m, null),
        new("Express Tünel Yıkama (Mevlana Petrol)", SeedFlavor.Robotik, "Antalya", "Muratpaşa", "Yenigün Mah.", 36.894219, 30.715305, 4.7m, null),
        new("Tornado Robotik Yıkama (Barınaklar)", SeedFlavor.Robotik, "Antalya", "Muratpaşa", "Çağlayan, Barınaklar Blv. 53", 36.851861, 30.769868, 2.6m, "+90 541 137 88 87"),
        new("Tornado Robotik Yıkama (Gazi Blv.)", SeedFlavor.Robotik, "Antalya", "Muratpaşa", "Konuksever, Gazi Blv. Yanyolu 5", 36.912750, 30.714580, 3.2m, "+90 541 137 88 87"),
        new("Opet (Etiler)", SeedFlavor.Jetonlu, "Antalya", "Muratpaşa", "Etiler, Emrah Cd. 2", 36.900100, 30.706400, 4.2m, "+90 444 6738"),
        new("Opet (Vatan Blv.)", SeedFlavor.Jetonlu, "Antalya", "Muratpaşa", "Güvenlik, Vatan Blv. 41", 36.903243, 30.684546, 4.2m, "+90 444 6738"),
        new("Opet - Enka Petrol", SeedFlavor.Jetonlu, "Antalya", "Muratpaşa", "Çağlayan, Bülent Ecevit Blv. 151", 36.858800, 30.775800, 4.4m, "+90 444 6738"),
        new("Opet Konuksever", SeedFlavor.Jetonlu, "Antalya", "Muratpaşa", "Konuksever, Karacaoğlan Cd. 73", 36.904917, 30.713973, 4.4m, null),
        new("Petrol Ofisi (Barınaklar)", SeedFlavor.Karma, "Antalya", "Muratpaşa", "Çağlayan, Barınaklar Blv. 53", 36.851906, 30.769605, 4.0m, "+90 800 211 0229"),
        new("Petrol Ofisi (Güllük Cd.)", SeedFlavor.Jetonlu, "Antalya", "Muratpaşa", "Üçgen, Güllük Cd. 76", 36.892632, 30.693958, 3.7m, "+90 800 211 0229"),
        new("Petrol Ofisi (Meydankavağı)", SeedFlavor.Jetonlu, "Antalya", "Muratpaşa", "Meydankavağı, Perge Blv. 70", 36.875656, 30.733617, 4.0m, "+90 800 211 0229"),
        new("Petrol Ofisi (Yenigöl)", SeedFlavor.Jetonlu, "Antalya", "Muratpaşa", "Yenigöl, Serik Cd. 36", 36.910623, 30.772718, 4.2m, "+90 850 226 8373"),
        new("Total (Etiler)", SeedFlavor.Jetonlu, "Antalya", "Muratpaşa", "Etiler, Evliya Çelebi Cd. 19", 36.898124, 30.709197, 3.9m, "+90 242 312 77 96")
    ];

    public static StationType ToStationType(this SeedFlavor flavor) => flavor switch
    {
        SeedFlavor.ElleYikama => StationType.FullService,
        SeedFlavor.Karma => StationType.Both,
        _ => StationType.SelfService
    };
}
