MODÜLER OK
AFJ_Direction_Arrow_Modular.fbx: üç parçalı bütün model.
Head_Left / Shaft / Head_Right: sol uç, çizgi, sağ uç.
Her parçanın beyaz yüzeyi ve siyah kenarlığı birlikte hareket eder.
Yalnızca Shaft nesnesinin yerel X ölçeğini değiştirin; uçları ölçeklemeyin.
Başlangıç toplam uzunluk 0.72; çizgi uzunluğu 0.08.
İstenen toplam uzunluk L (L >= 0.72):
Shaft localScale.x = (L - 0.64) / 0.08
Head_Left localPosition.x = -(L - 0.64) / 2
Head_Right localPosition.x = (L - 0.64) / 2
Diğer eksenleri değiştirmeyin. Aynı üst nesneyi döndürerek dikey kullanın.
Ayrı modül FBX'leri de sağlanmıştır. FBX eksenleri Unity için -Z forward, Y up.
Önizlemedeki mor bloklar FBX ok modeline dahil değildir.
Unity projesine otomatik bağlanmadı.
