"""Gizli (ödül) resim sahneleri: her bölümün piksel resminden yola çıkan tatlı bir uzay sahnesi ve o resme ayrı uzay zemini.
Piksel resim yalnız konu ve renk kaynağıdır; sahne yeni bir duruş ve kompozisyon ister. Agresif, yıkıcı ya da korkutucu konu yok.
SCENES: bölüm → (Türkçe özet, ana konu, İngilizce sahne)."""
import colorsys
from collections import Counter

from .model import PALETTE

SCENES = {
    1: ('İki renkli çerçeve, yıldızlara açılan küçük bir pencere; önünde minik astronot el sallıyor', 'a two-color square frame', 'A big two-color square frame floats in space like a little window to the stars. A tiny astronaut sits on its lower edge and waves.'),
    2: ('Yıldızlardan oluşan bir uçurtma, ipini minik bir uzaylı tutuyor', 'a star pattern', 'A tiny friendly alien on a small moon flies a big kite covered with a pattern of stars.'),
    3: ('Sarmal şeklinde renkli bir lolipop gezegen, etrafında uydular', 'a colorful spiral', 'A round planet painted with a big colorful spiral, like a lollipop, turns slowly with two tiny moons around it.'),
    4: ('Zikzak desenli bir roket yıldızlar arasında süzülüyor', 'a zigzag pattern', 'A chubby little rocket painted with bold zigzag stripes cruises between the stars.'),
    5: ('Dama desenli bir uzay battaniyesinde piknik yapan iki minik uzaylı', 'a checkerboard', 'Two tiny friendly aliens have a picnic on a big checkered blanket spread over a small moon.'),
    6: ('Hamburger bir gezegen gibi süzülüyor, minik astronot üstüne turşu yerleştiriyor', 'a cheeseburger', 'A giant cheeseburger floats in space like a planet, while a tiny astronaut gently places a pickle on top.'),
    7: ('Taç küçük bir gezegenin tepesinde, etrafında mücevher uydular', 'a royal crown', 'A golden crown sits on top of a tiny round planet, with small gems orbiting it like moons.'),
    8: ('Büyük kırmızı elma minik gezegenin üstünde, yanındaki minik astronot ona merdiven dayıyor', 'a red apple', 'A very big shiny red apple with a leaf sits on top of a tiny grassy planet and fills most of the picture, while a very small astronaut leans a little ladder against it.'),
    9: ('Palyaço balığı kasklı, yıldızlar arasında yüzüyor', 'a clownfish', 'A cheerful clownfish wearing a round glass space helmet full of water swims between the stars, followed by a trail of bubbles.'),
    10: ('Bulut, şimşeğiyle minik deniz fenerinin lambasını yakıyor', 'a storm cloud with a lightning bolt', 'A soft faceless cloud floats above a tiny round moon. A big, clearly visible yellow lightning bolt gently touches the lamp of a tiny lighthouse on the moon and switches its light on.'),
    11: ('Tilki astronot ayda kuyruğuna kıvrılıp yıldızları izliyor', 'a fox', 'An orange fox astronaut sits on a small moon with its fluffy tail curled around it, looking through a small telescope.'),
    12: ('Armut şeklinde bir uzay kapsülü, penceresinden minik solucan bakıyor', 'a green pear', 'A green pear-shaped space capsule with a round window and a leaf on top drifts along, and a tiny worm astronaut waves from the window.'),
    13: ('İksir şişesinden yıldızlar çıkıyor, minik büyücü uzaylı izliyor', 'a magic potion bottle', 'A magic potion bottle stands on a small moon and gently pours out tiny stars, while a little alien in a wizard hat watches happily.'),
    14: ('Üzüm salkımı bir gezegen sistemi gibi, taneleri uydu', 'a bunch of purple grapes', 'A big bunch of purple grapes floats like a cluster of little planets, and a tiny rocket flies around it with a green leaf in tow.'),
    15: ('Denizanası uzay balonu gibi süzülüyor, altında minik astronot', 'a jellyfish', 'A pink and purple jellyfish floats through space like a hot-air balloon, carrying a tiny astronaut who holds on to its soft tentacles.'),
    16: ('Tek gül küçük bir gezegende, minik astronot sulama kabıyla suluyor', 'a red rose', 'A single red rose grows on a tiny round planet, and a small astronaut waters it with a little watering can.'),
    17: ('Pizza dilimi uçan daire gibi süzülüyor, uzaylılar peynir uzatıyor', 'a pizza slice', 'A big pizza slice flies through space like a spaceship, and two tiny aliens ride on it, sharing a pepperoni.'),
    18: ('Lale bahçeli minik gezegen, arı astronot uçuyor', 'a tulip', 'A big red tulip grows on a tiny grassy planet with a few smaller tulips around it, and a little bee in a space helmet flies toward it.'),
    19: ('Roket halkalı gezegenin etrafında dönüyor, arkasında küçük kalp izi', 'a rocket ship', 'A big red and white rocket loops around a small ringed planet, leaving a thin trail with one very small heart shape in it. The rocket is by far the largest thing in the picture.'),
    20: ('Panda astronot ay üstünde bambu yiyor', 'a panda', 'A round panda astronaut sits on a small moon happily eating bamboo that grows from a crater.'),
    21: ('Ayçiçeği güneşe bakan küçük bir gezegende büyüyor', 'a sunflower', 'A tall sunflower grows on a tiny planet and turns its face toward a small friendly star, while a tiny astronaut leans against its stem.'),
    22: ('İç içe kareler renkli bir uzay kapısı, roket içinden geçiyor', 'nested colorful squares', 'Nested colorful squares form a big space gate, and a tiny rocket flies through its center.'),
    23: ('İnek ayın üzerinden zıplıyor', 'a cow', 'A happy black and white cow in a little space helmet jumps gracefully over a crescent moon.'),
    24: ('Muz şeklinde bir uzay gemisi, minik maymun pilot', 'a banana', 'A yellow banana-shaped spaceship cruises along with a tiny monkey pilot waving from its cockpit.'),
    25: ('Uğur böceği küçük bir yaprak gezegende, sırtındaki benekler yıldız gibi', 'a ladybug', 'A red ladybug walks along a big leaf floating in space, with little star-shaped spots on its back.'),
    26: ('Meteor nazikçe süzülüyor, üstünde sörf yapan minik uzaylı', 'a warm orange meteor', 'A friendly orange meteor glides through space with a soft flat tail, and a tiny alien surfs on top of it.'),
    27: ('Yeşil uzaylı küçük gezegeninde çiçek ekiyor', 'a green alien', 'A cute green alien kneels on its tiny planet, planting a small flower beside its little round house.'),
    28: ('Yıldız ay üstünde oturup minik gezegenlerle sohbet ediyor', 'a smiling star', 'A smiling yellow star sits on a crescent moon and swings its legs, with tiny planets around it.'),
    29: ('Süt kutusu roket gibi uçuyor, kedicik peşinden gidiyor', 'a milk carton', 'A milk carton flies like a little rocket, and a tiny kitten astronaut happily floats after it.'),
    30: ('Portakal bir gezegen gibi, üstünde küçük ev ve ağaç', 'an orange fruit', 'A big orange fruit floats like a planet, with a tiny house and a leaf-shaped tree standing on top.'),
    31: ('Kurbağa astronot nilüfer yaprağında süzülüyor', 'a frog', 'A green frog astronaut sits on a lily pad floating through space and looks at a tiny planet that resembles a fly.'),
    32: ('Kalpler balon gibi, minik astronot bir demet tutuyor', 'heart patterns', 'A tiny astronaut drifts through space holding a bunch of heart-shaped balloons in many colors.'),
    33: ('Robot küçük bir gezegeni sulayıp çiçek yetiştiriyor', 'a friendly robot', 'A friendly robot tends a small garden on a tiny planet, gently holding a flower pot in its hands.'),
    34: ('Gülen bulut minik bir gezegene nazikçe yağmur yağdırıp çiçek açtırıyor', 'a smiling cloud', 'A smiling white cloud gently rains on a tiny planet, and small flowers bloom below it.'),
    35: ('Çilek minik ayın üstünde sıcak bir ev, kapısı ve penceresi var', 'a strawberry', 'A big strawberry stands on a small moon as a cozy little house, with a round door, a window and tiny stars rising from its green top.'),
    36: ('Labirent desenli bir gezegen, minik rover yolunu buluyor', 'a maze', 'A round planet with a big maze pattern on it, and a tiny rover happily finding its way along the paths.'),
    37: ('Astronot yıldız yakalamak için ağ sallıyor', 'an astronaut', 'A cute astronaut floats in space and catches a small star with a butterfly net.'),
    38: ('Papağan uçuş gözlüğüyle yıldızlar arasında uçuyor, küçük bir gezegeni top gibi taşıyor', 'a parrot', 'A colorful parrot flies between the stars with wide open wings, wearing tiny flight goggles and carrying a small planet like a ball.'),
    39: ('Sevimli iskelet dans ederek gezegenin üstünde şarkı söylüyor', 'a cute skeleton', 'A cute, friendly cartoon skeleton in a party hat dances on a small moon, playing a little guitar. Not scary at all.'),
    40: ('Oyun kolu uzay gemisi, antenli minik uzaylı oynuyor', 'a game controller', 'A big game controller floats like a space station, and a tiny alien sits on it pressing a colorful button with both hands.'),
    41: ('Sevimli minik ninja, silahsız, hilal ayın üstünde çay içip dinleniyor', 'a little ninja', 'A cute little ninja with no weapons sits on a crescent moon, relaxing and sipping tea.'),
    42: ('Güneş küçük gezegenlere gözlük takıp selam veriyor', 'a smiling sun', 'A smiling sun wearing sunglasses waves at the tiny planets that orbit around it.'),
    43: ('Minik uzaylı renkli şemsiyesinin altında, etrafına kar gibi yumuşak yıldızlar yağıyor', 'an umbrella', 'A tiny alien stands on a small moon under a big colorful umbrella as soft little stars fall around it like snow.'),
    44: ('Balina yıldızlar arasında süzülüyor, sırtında minik köy', 'a whale', 'A big blue whale swims gracefully through space with a tiny village on its back and a spout of little stars rising above it.'),
    45: ('Mum minik ayda, uzay kıyafetli fare yanında patilerini ısıtıyor', 'a candle', 'A candle stands straight in a small holder on top of a tiny round moon. A tiny astronaut mouse sits beside it warming its paws. A small rocket passes in the distance.'),
    46: ('Dev dondurma külahı kule gibi, kenarında oturan astronot bir kaşık alıyor', 'an ice cream cone', 'A huge two-scoop ice cream cone floats in space like a tower, and a tiny astronaut sits on the rim of the cone enjoying a small spoonful.'),
    47: ('Balonlara bağlı minik ev gezegenler arasında uçuyor', 'a bunch of balloons', 'A big bunch of colorful balloons carries a tiny house up between the planets.'),
    48: ('Kelebek gezegenden gezegene uçuyor, kanatlarında yıldız deseni', 'a butterfly', 'A purple and turquoise butterfly flies between tiny flower-covered planets, with star patterns on its wings.'),
    49: ('Domuzcuk astronot, kanatlı kostümüyle uçuyor', 'a piglet', 'A round pink piglet in a space suit with little wings on its backpack flies happily past a planet.'),
    50: ('Astronot kaskı akvaryum olmuş, içinde minik balık', 'an astronaut helmet', 'An empty astronaut helmet rests on a small moon like an aquarium, with a tiny goldfish swimming inside and a plant growing beside it.'),
    51: ('Güneş gözlüklü ananas minik plaj gezegeninde, yanında şezlong ve top', 'a pineapple', 'A big pineapple wearing sunglasses stands on a tiny beach planet, next to a small deck chair and a beach ball.'),
    52: ('Arı astronot, çiçek gezegenden bal topluyor', 'a bee', 'A chubby bee in a tiny space helmet collects nectar from a big flower growing on a tiny planet.'),
    53: ('Oyuncak tren halkalı gezegenin halkasında gidiyor', 'a toy train', 'A little toy train rides along the ring of a big ringed planet as if it were a railway, puffing small round clouds.'),
    54: ('Yaprak uzay teknesi, minik karınca kaptan', 'an autumn leaf', 'A big autumn leaf floats through space like a little boat, with a tiny ant captain steering it.'),
    55: ('Kaplumbağa sırtında küçük bir dünya taşıyor', 'a sea turtle', 'A gentle sea turtle swims through space carrying a tiny round world with trees on its shell.'),
    56: ('Balkabağı minik ev, penceresinden sıcak ışık; korkutucu değil', 'a carved pumpkin', 'A big friendly carved pumpkin with a gentle smile serves as a cozy little house on a tiny moon, with warm light in its windows and a tiny alien at the door.'),
    57: ('Çaydanlık uzayda çay dolduruyor, buharı küçük bulutlar', 'a teapot', 'A teapot floats in space and pours tea into a small cup resting on a tiny moon, with round puffs of steam rising from it.'),
    58: ('Baykuş ay üstünde teleskopla bakıyor', 'an owl', 'A round owl with big eyes sits on a crescent moon and looks at the stars through a small telescope.'),
    59: ('Gökkuşağı iki gezegen arasında köprü, üstünde minik astronot yürüyor', 'a rainbow with clouds', 'A rainbow forms a bridge between two tiny planets with fluffy clouds at each end, and a tiny astronaut walks across it.'),
    60: ('Tekir kedi astronot gemisinin dışında kuyruklu yıldızla oynuyor', 'a gray tabby cat', 'A happy gray tabby cat astronaut, seen in full body from the side, floats outside a small round spaceship and plays with a little comet, reaching for it with one paw while the other arm hangs relaxed. Its tether curls behind it.'),
    61: ('Avokado yarısı minik bir gezegen gibi, çekirdeği top; iki uzaylı oynuyor', 'an avocado half', 'A big avocado half lies on a small moon like a little pool, and two tiny aliens roll its round pit like a ball.'),
    62: ('Kaktüs minik çöl gezegeninde, tepesinde çiçek açmış', 'a potted cactus', 'A potted cactus with a bright flower stands on a tiny sandy planet, with a small astronaut watering it from a safe distance.'),
    63: ('Flamingo halka gezegende tek ayak üstünde duruyor', 'a flamingo', 'A pink flamingo stands on one leg on the ring of a small planet, as if it were standing in shallow water.'),
    64: ('Şeritli balon kapsül gezegenler arasında süzülüyor', 'stripes', 'A hot-air balloon with bold stripes floats between small planets, with a tiny astronaut in its basket.'),
    65: ('Donut halkalı gezegen gibi, şekerleri uydu; minik astronot yürüyor', 'a pink donut', 'A big pink frosted donut floats in space like a ringed planet, with sprinkles around it and a tiny astronaut walking across the frosting.'),
    66: ('Oyuncak ayı astronot bir yıldıza sarılıyor', 'a teddy bear', 'A purple teddy bear in a small space suit floats in space, happily hugging a small star.'),
    67: ('UFO ışığıyla filizi büyütüyor, inek mutlu bakıyor', 'a UFO', 'A UFO seen from below at a tilted angle hovers over a tiny round planet and shines a soft beam of light on a small sprout, helping it grow. A happy cow on the planet looks up at it. The UFO keeps the sprite colors: a turquoise dome, green side panels, a gray body and yellow and orange lights.'),
    68: ('Küçük volkan yıldız ve yuvarlak bulutlar üflüyor, astronot yanında marşmelov kızartıyor', 'a small volcano', 'A small friendly volcano on a tiny planet gently puffs out little stars and round clouds, while a tiny astronaut roasts a marshmallow nearby.'),
    69: ('Limon dilimi güneş gibi parlıyor, minik uzaylı limonata içiyor', 'a lemon', 'A big lemon and a lemon slice float side by side, and a tiny alien sips lemonade with a straw from the lemon.'),
    70: ('Tavşan astronot ay üstünde havuç yetiştiriyor', 'a bunny', 'A white bunny astronaut hops on a small moon, tending a tiny row of carrots growing from a crater.'),
    71: ('Suşi rulo minik uzay gemisi, üstünde çubuklarla kürek çeken astronot', 'a sushi roll', 'A cute sushi roll flies like a small spaceship, with a tiny astronaut sitting on top holding chopsticks like oars.'),
    72: ('Zürafa boynuyla gezegenden yıldızlara uzanıyor, yıldızları asıyor', 'a giraffe', 'A tall giraffe stands on a tiny planet and uses its long neck to gently hang a small star in the sky.'),
    73: ('Hilal ay uyuyor, minik uzaylı ona battaniye örtüyor', 'a crescent moon', 'A sleepy crescent moon with a nightcap dozes in space, and a tiny alien tucks a small blanket around it.'),
    74: ('Ampul minik gezegenin sokak lambası, altında kitap okuyan uzaylı', 'a light bulb', 'A big light bulb stands like a street lamp on a tiny planet, and a little alien sits under it reading a book.'),
    75: ('Yuvarlak ağaç küçük gezegende, dallarında salıncak ve minik ev', 'a round tree', 'A big round tree covers a tiny planet, with a little tree house and a swing hanging from its branch.'),
    76: ('Dünya uzaydan görünüyor, minik astronot el sallıyor', 'the planet Earth', 'The planet Earth seen from space with a small moon nearby, and a tiny astronaut floating in front of it, waving at it.'),
    77: ('Deniz kabuğu ayda ev olmuş, içinden yengeç astronot çıkıyor', 'a seashell', 'A big pink seashell rests on a small moon like a cozy house, and a tiny crab in a space helmet peeks out of it.'),
    78: ('Kahve kupası süzülüyor, buharı küçük bulutlar gibi, astronot kulpunda dinleniyor', 'a coffee mug', 'A big white coffee mug floats in space with swirls of steam shaped like small clouds, and a tiny astronaut relaxes on its handle.'),
    79: ('Cadı şapkasından yıldızlar ve kediler çıkıyor; korkutucu değil', 'a witch hat', 'A purple witch hat floats in space and tiny stars and a small friendly kitten rise out of it like a magic trick.'),
    80: ('Kar tanesi yıldızların arasında süzülüyor, minik uzaylı yakalıyor', 'a snowflake', 'A big snowflake drifts through space, and a tiny alien on a small icy moon reaches out with its tongue to catch it.'),
    81: ('Kara delik sevimli bir girdap, minik gezegenler onun etrafında dans ediyor', 'a swirling purple whirl', 'A cute swirling purple whirl sits in space like a spinning top, with tiny planets dancing around it in a playful circle.'),
    82: ('Tuğla duvarı minik uzaylılar renkli renkli örüyor', 'a colorful brick wall', 'Two tiny aliens stack colorful bricks into a little wall on a small moon, with a trowel and a small bucket beside them.'),
    83: ('Aslan astronot yelesi yıldızlı, ay üstünde oturuyor', 'a lion', 'A friendly lion astronaut sits on a small moon, with little stars caught in its big fluffy mane.'),
    84: ('Patlamış mısır kutusu; tanecikler yıldız gibi yukarı süzülüyor', 'a box of popcorn', 'A striped box of popcorn floats in space while pieces of popcorn drift upward and become little stars.'),
    85: ('Küçük araba halkalı gezegenin halkasında gidiyor', 'a little car', 'A cute little red car drives on the ring of a planet with a tiny astronaut at the wheel and a suitcase on its roof.'),
    86: ('Mandala desenli yuvarlak bir gezegen, çevresinde minik uydular', 'a mandala', 'A round planet covered with a big symmetrical mandala pattern in the colors of the sprite, seen straight on, with small moons orbiting it in a neat circle and many small stars around.'),
    87: ('Ahtapot sekiz koluyla minik gezegenlerle hokkabazlık yapıyor', 'an octopus', 'A purple octopus in a round glass helmet juggles tiny planets with its arms.'),
    88: ('Kardan adam karlı minik gezegende, atkısı uçuşuyor', 'a snowman', 'A snowman with a red scarf stands on a tiny snowy planet, with a little astronaut adding a carrot nose.'),
    89: ('Karpuz dilimi kayık olmuş, astronotlar kürek çekiyor, çekirdekler yıldız gibi süzülüyor', 'a watermelon slice', 'A big watermelon slice floats like a little boat, with tiny astronauts rowing it and seeds drifting away like dark little stars.'),
    90: ('Kırmızı mantar minik ev, kapısında uzaylı', 'a red mushroom', 'A big red mushroom with white spots stands on a tiny moon as a cozy house, with a small door, a window and a tiny alien waving at the door.'),
    91: ('Elmas ay yüzeyinde parlıyor, minik gezgin rover buluyor', 'a diamond', 'A big faceted diamond sits on a small moon, and a tiny rover with a little flag has just found it.'),
    92: ('Dört yapraklı yonca minik gezegende; uğur böceği astronot', 'a four-leaf clover', 'A big four-leaf clover grows on a tiny grassy planet, and a little ladybug astronaut sits on one of its leaves.'),
    93: ('Hediye kutusundan minik gezegenler çıkıyor', 'a gift box', 'A gift box with a big bow opens in space, and tiny planets and stars float out of it.'),
    94: ('Çapa minik uzay teknesini aya bağlıyor', 'an anchor', 'An anchor on a long rope holds a little space boat in place, hooked gently onto a small crescent moon.'),
    95: ('Halkalı gezegende minik astronot paten kayıyor', 'a ringed planet', 'A big ringed planet in warm orange tones, with a tiny astronaut ice-skating along its ring.'),
    96: ('Penguen astronot buz ayında kayıyor', 'a penguin', 'A penguin astronaut slides on its belly across a small icy moon with a big happy expression.'),
    97: ('Noel ağacı minik gezegende, süsleri gezegenler', 'a Christmas tree', 'A decorated Christmas tree stands on a tiny snowy planet, with tiny planets for ornaments and a star on top.'),
    98: ('Yavru dinozor yumurtadan çıkmış, ay üstünde yıldıza bakıyor', 'a baby dinosaur', 'A cute baby dinosaur sits on a small moon beside its cracked eggshell, reaching up curiously toward a tiny star.'),
    99: ('Yelkenli yıldız denizinde, yelkeni rüzgârla dolu', 'a sailboat', 'A little sailboat sails on a sea of small stars, with a tiny astronaut at the wheel and a planet behind it.'),
    100: ('Fil astronot hortumuyla yıldız tozu püskürtüyor', 'an elephant', 'A gray elephant astronaut on a small moon gently sprays a little fountain of stars from its trunk.'),
}

# Uzay zeminleri: (kısa ad, ton (derece), İngilizce tarif). Hepsi koyu; piksel resmin ana tonundan uzak olan seçilir.
BACKS = [
    ('koyu turkuaz dalgalar', 180, 'a deep teal sky with one wide wavy band of lighter teal crossing it, small round pale mint dot stars, and 2 small cream and coral planets'),
    ('mor, macenta bulutlar', 300, 'a deep plum purple sky with two big soft flat cloud shapes of lighter magenta, small pink and cream four-pointed stars, and 2 small mint and orange planets'),
    ('bordo bantlar', 340, 'a dark burgundy and aubergine sky divided by large flat curved color bands, small golden five-pointed stars, a thin crescent moon and 1 small turquoise planet'),
    ('çok koyu kobalt, asteroit kuşağı', 220, 'a very dark navy-cobalt sky, close to black-blue and not bright, with a curved belt of tiny gray asteroids, small white sparkle stars and 2 small lilac and pink ringed planets'),
    ('gece mavisi, süt yolu', 235, 'a midnight navy sky with a broad diagonal band of small pale dots like a milky way, and 2 small yellow and pink planets'),
    ('koyu orman yeşili', 150, 'a very dark forest green sky with wide flat swirls of lighter green, small cream dot stars and 2 small orange and lilac planets'),
    ('çivit, lila halka', 260, 'a dark indigo sky with small lilac four-pointed stars, a large faint flat ring shape in the corner and 2 small peach and aqua planets'),
    ('patlıcan moru, şeftali', 285, 'a dark aubergine sky with small peach sparkle stars, a few small flat comet streaks and 1 small mint ringed planet'),
    ('arduvaz mavisi, takımyıldız', 205, 'a dark slate blue sky with a few simple constellations of small white stars joined by thin lines, and 2 small coral and yellow planets'),
    ('sıcak kahve-kızıl', 15, 'a dark warm maroon-brown sky with small orange and cream stars, a big thin crescent moon and 1 small teal planet'),
    ('derin okyanus mavisi', 195, 'a deep ocean blue sky with small round cyan dot stars, soft flat wave-shaped bands of darker blue and 2 small pink and yellow planets'),
    ('koyu menekşe, pembe kuyruklu', 275, 'a dark violet sky with small white dot stars, two small pink comets with flat tails and 1 small green planet'),
]
BACK_OVERRIDE = {10: 0, 60: 1, 45: 2, 67: 3}    # beğenilen örneklerin zemini


def _hue(rows) -> float:
    """Resmin ana tonu (derece); griler sayılmaz. Renksiz resimde -1."""
    counts = Counter(c for r in rows for c in r if c != '.')
    x = y = 0.0
    import math
    for code, k in counts.items():
        h, l, s = colorsys.rgb_to_hls(*PALETTE[code][1])
        if s < 0.2: continue
        x += k * s * math.cos(2 * math.pi * h); y += k * s * math.sin(2 * math.pi * h)
    if not x and not y: return -1.0
    return math.degrees(math.atan2(y, x)) % 360


def pick_backs(rows_of: dict) -> dict:
    """Bölüm → zemin sırası: ana tondan uzak, son 5 bölümde kullanılmamış."""
    out, recent = {}, []
    def dist(a, b): return min(abs(a - b), 360 - abs(a - b))
    for n in sorted(rows_of):
        if n in BACK_OVERRIDE:
            k = BACK_OVERRIDE[n]
        else:
            hue = _hue(rows_of[n])
            opts = [i for i in range(len(BACKS)) if i not in recent[-5:]]
            k = max(opts, key=lambda i: (0 if hue < 0 else min(dist(BACKS[i][1], hue), 90), -recent.count(i)))
        out[n] = k; recent.append(k)
    return out
