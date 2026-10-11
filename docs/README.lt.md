# ShuffleDrop 0.3

„Windows“ muzikos tvarkyklė „iPod Shuffle“, veikianti be „iTunes“.

## Paleidimas

Paleisk ShuffleDrop-Setup-0.3.0.exe. Diegiklis įdiegia programą tavo Windows paskyrai, sukuria pradžios meniu nuorodą ir pasirinktinai nuorodą darbalaukyje. Administratoriaus teisių nereikia. Pašalinti galima per Windows programų sąrašą; muzikos atsarginės kopijos ir temos pasirinkimas išlieka.

Jeigu naudoji nešiojamą ZIP versiją, išskleisk visą archyvą ir paleisk ShuffleDrop.exe. Greta turi likti visi pateikti DLL ir ShuffleDrop.ico failai. Reikalinga .NET Framework 4.7.2 ar naujesnė versija. Garso atkūrimui Windows N leidimuose gali reikėti Microsoft Media Feature Pack.

Prijunk grotuvą. Skiltyje **Biblioteka** automatiškai pasirodo esamos dainos, atlikėjai, albumai, trukmė ir dydis. Jei failas neturi žymų, rodomas failo vardas.

**Baterija:** šoninėje juostoje rodoma „Baterijos % nepalaikomas“, nes šioje versijoje nėra patikimo „Shuffle“ baterijos procento ar įkrovimo būsenos skaitymo. Paspausk užrašą ir matysi Apple lemputės / VoiceOver paaiškinimą 2 ir 4 kartos modeliams. USB prijungimas savaime nepatvirtina įkrovimo; kompiuterio baterijos lygis nenaudojamas.

- **Paieška** ieško pagal dainą, atlikėją ir albumą. Stulpelio antraštė rūšiuoja matomą sąrašą. Ctrl+A pažymi visas matomas dainas.
- **Groti** arba dvigubas paspaudimas paleidžia pasirinktą dainą per kompiuterį. Atkūrimas naudoja NAudio ir Windows Media Foundation / WASAPI; MCI pašalintas. Yra pauzė, tęsimas, stabdymas, dainos eiga ir garsumas. Spustelėk eigos juostą, kad peršoktum į kitą vietą. Rodyklių klavišai veikia sufokusavus juostą.
- **Į kompiuterį** nukopijuoja pasirinktas dainas su suprantamais failų vardais. Esamų failų neperrašo.
- **Šalinti** pirmiausia išsaugo ir patikrina dainų kopijas kompiuteryje, tada pašalina jų įrašus ir failus iš grotuvo.
- **↑ / ↓** keičia grojimo tvarką. Išvalyk paiešką ir grąžink rūšiavimą pagal # didėjimo tvarka. Spausk **Išsaugoti tvarką**. Atsitiktinio grojimo režime ši tvarka netaikoma.
- **Atkurti** atšaukia paskutinę operaciją arba atkuria nutrūkusį įrašymą. Vėlesnis pakeitimas pakeičia atkūrimo tašką. Pašalintų dainų atkūrimui naudok tą patį kompiuterį ir Windows paskyrą.
- Skiltyje **Pridėti muziką** pasirink MP3 failus / aplanką arba nutempk juos į eilę, tada spausk **Perkelti į iPod**. Turinio dublikatai praleidžiami.

Pabaigoje saugiai išimk grotuvą per Windows. Operacijos metu uždaryk kitas grotuvą valdančias programas.

Temos jungiklis šoninės juostos apačioje perjungia šviesų / tamsų režimą. Pasirinkimas išsaugomas %LOCALAPPDATA%\ShuffleDrop\theme.txt. Programa turi naują kelių raiškų ikoną ir albumo viršelį atkūrimo juostoje, jei jis yra dainos faile.

## Atsarginės kopijos ir ribos

Kopijos saugomos %LOCALAPPDATA%\ShuffleDrop\Backups, šalinamų dainų failai – jo Removed poaplankyje. Jos automatiškai nevalomos. Šalinimui reikia vietos kompiuteryje. Grotuvo operacijų istorija yra iPod_Control\ShuffleDrop\transactions.

Prieš paskutinį įrašymo etapą galima atšaukti operaciją. Prasidėjus šiam etapui programa jį užbaigia. Po atjungimo įrašymo metu vėl prijunk grotuvą ir naudok **Atkurti**. Po importo atšaukimo / atkūrimo grotuve gali likti į sąrašą neįtrauktų naujų failų; automatinis jų valymas nenumatytas.

Reikalingas jau paruoštas FAT / FAT32 „Shuffle“ su palaikomu iTunesSD sąrašu. Palaikomi 1 / 2 ir 3 / 4 kartų sąrašo formatai. Programa neformatuoja grotuvo ir neatkuria jo gamyklinės programinės įrangos.

Importuojami MP3 (MPEG Layer III, bent 16 kHz). FLAC / M4A konvertavimo, žymų redagavimo ir naujų grojaraščių kūrimo nėra. Esamus kitų formatų failus galima matyti ir eksportuoti. Nauji VoiceOver įrašai negeneruojami. Mišrios 3 / 4 kartos bibliotekos su tinklalaidėmis / audioknygomis šalinimas nepalaikomas.

Keičiamas iTunesSD, o ne iTunesDB. Vėlesnis sinchronizavimas per iTunes ar kitą programą gali pakeisti biblioteką. Grojimo / statistikos podėliai atnaujinami; atsitiktinė tvarka gali pasikeisti.

Tai bandomoji versija. Duomenų ir failų operacijos patikrintos atskiruose bandomuosiuose aplankuose, įskaitant gedimus po šalinimo ir tikslų atkūrimą. Tikrame prijungtame grotuve perskaityta 56 dainų biblioteka; jo sąrašas nepakeistas. Tikras naujai įrašytos muzikos grojimas pačiame įrenginyje dar nepatvirtintas.

## Kūrimas

build.ps1 sukuria programą su Windows .NET Framework kompiliatoriumi. tests/run.ps1 vykdo duomenų, failų, atkūrimo ir metaduomenų bandymus atskiruose tests/run aplankuose. Neprivalomi argumentai: vietinė iTunesSD kopija ir tik skaitymui naudojamas grotuvo diskas. tests/UITests.cs tikrina paiešką ir tvarkos pakeitimus lange, jų neįrašo į grotuvą. --render-preview <png> pateikia tik skaitomą bibliotekos lango peržiūrą.

Programos kodas: MIT. Dainų informacijai skaitoma atskira TagLibSharp 2.3.0 biblioteka (LGPL-2.1). Jos licencija ir atitinkami šaltiniai pateikti third-party aplanke. Biblioteką galima pakeisti suderinama savo sukompiliuota versija; jos modifikavimo ir derinimo teisės neribojamos. Formato šaltiniai: SOURCES.md.

Sąsaja: Krypton.Toolkit 90.25.2.55 (BSD-3-Clause), garso atkūrimas: NAudio.Core / NAudio.Wasapi 2.2.1 (MIT). Licencijos yra third-party aplanke. Garso bandymai su tikru MP3 patvirtino dekodavimą, nutildytą WASAPI atkūrimą, pauzę, tęsimą ir peršokimą; į grotuvą nerašyta.
