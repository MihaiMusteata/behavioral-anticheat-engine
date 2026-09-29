# Structura tezei de master și a Practicii de cercetare

**Tema:** *Analiza factorilor asociați fraudei academice în evaluările online pentru identificarea comportamentelor suspecte*

**Program:** Tehnologia Informației (TI), FCIM, UTM
**Sistem dezvoltat:** Behavioral Anticheat Engine (prototip funcțional)

---

## 1. Baza de referință: structuri reale din repozitoriul UTM

Am analizat colecția de proiecte de master TI/FCIM 2026 din IRTUM
([handle/5014/35566](https://repository.utm.md/handle/5014/35566), 27 lucrări) și am extras
cuprinsurile complete a trei teze reprezentative, alese pentru apropierea structurală de tema ta.

### 1.1 Structura-cadru comună (obligatorie)

Toate cele trei teze respectă exact același schelet:

| Secțiune | Observații |
|---|---|
| Foaie de titlu | „Admis la susținere / Șef departament: FIODOROV Ion dr., conf.univ." + Student / Coordonator / Consultant |
| **REZUMAT** | ~1 pagină, în română; **rezumă capitol cu capitol**, explicit („În capitolul 1, ... s-au prezentat...") |
| **ABSTRACT** | traducerea fidelă în engleză a rezumatului |
| **CUPRINS** | numerotare fără punct după cifra capitolului (`1 FUNDAMENTAREA...`, `1.1 Noțiuni...`) |
| **ABREVIERI ȘI DEFINIȚII** | listă extinsă: acronime **și** definiții de termeni-cheie (1–2 pagini) |
| **INTRODUCERE** | context → problemă → scopul lucrării → **descrierea structurii pe capitole** |
| 3–4 capitole numerotate | vezi variantele de mai jos |
| **CONCLUZII** | |
| **BIBLIOGRAFIE** | 30–50 surse, stil IEEE-like numerotat |
| **ANEXE** | opțional (`ANEXA A`) |

**Volum:** 55–80 pagini.

### 1.2 Cele trei variante de organizare a capitolelor

**Varianta A — „analiză + dezvoltare componentă"** (Negrea I., *Analiza și dezvoltarea componentelor client ale sistemului de autentificare cu doi factori*):

```
1 FUNDAMENTAREA TEORETICĂ A ... (5 subcapitole)
2 ANALIZA SOLUȚIILOR EXISTENTE ȘI DEFINIREA CERINȚELOR SISTEMULUI PROPUS (4)
3 ANALIZA ȘI PROIECTAREA COMPONENTELOR ... (6, cu propunerea soluției originale)
4 DEZVOLTAREA ȘI TESTAREA COMPONENTELOR ... (7, incl. validare + valoare practică)
```

**Varianta B — „modelare statistică / analiză de date"** (Buza D., *Modelarea statistică a solicitărilor din bazele de date*) — **cea mai apropiată de partea analitică a temei tale**:

```
1 FUNDAMENTE TEORETICE (5)
2 CERCETAREA METODOLOGIILOR DE ANALIZĂ ... (4)
   2.1 Descrierea metodologiei de colectare a datelor și procesarea datelor
   2.2 Modelarea ...
   2.3 Validarea ...
   2.4 Analiza inferențială și stabilitatea estimatorilor
3 APLICAȚII PRACTICE ALE MODELULUI ... (6)
   3.1 Studiu de caz   3.2 Rezultatele experimentale   3.3 Analiza rezultatelor
   3.4 Optimizarea ...  3.5 Comparare cu alte modele   3.6 Concluzii și recomandări
```

**Varianta C — „cercetare experimentală + sistem"** (Ursu R., *Analiza influenței compilării anticipate și dinamice asupra performanței microserviciilor*):

```
1 <TEORIA TEHNOLOGIEI STUDIATE> (5)
2 <CONTEXTUL APLICATIV / DOMENIUL> (5)
3 CERCETARE APLICATIVĂ (4)
   3.1 Implementarea experimentală  3.2 Rezultatele obținute
   3.3 Automatizarea testelor       3.4 Validitatea rezultatelor și limitările cercetării
4 DEZVOLTAREA SISTEMULUI (3)
```

### 1.3 Observații practice desprinse din analiză

- **Titlurile din promoția 2026 încep majoritar cu „Analiza..."** — titlul tău se încadrează perfect în normele departamentului.
- Bibliografiile conțin **masiv publicații ale cadrelor didactice UTM** (Peca, Cojocaru, Ciorbă, Fiodorov, Alexei, Bolun, Țurcanu, Istrati ș.a.). Pare a fi o așteptare tacită a departamentului — merită să incluzi lucrări relevante ale coordonatorului/departamentului.
- Varianta B demonstrează că **un capitol dedicat metodologiei de colectare și procesare a datelor este acceptat și încurajat** — exact ce îți trebuie pentru componenta de analiză.
- Varianta C include explicit **„Validitatea rezultatelor și limitările cercetării"** — secțiune pe care ți-o recomand, dată fiind eticheta auto-raportată (zgomotoasă) din sistemul tău.

---

## 2. Structura propusă pentru TEZA FINALĂ

Structura de mai jos combină **Varianta A** (pentru partea de proiectare/dezvoltare) cu **Varianta B**
(pentru partea de analiză a datelor), pentru că tema ta promite ambele: *„Analiza factorilor..."*
(componenta analitică) *„...pentru identificarea comportamentelor suspecte"* (componenta de sistem).

> **Capitolul 4 este cel care onorează promisiunea din titlu.** Fără el, lucrarea rămâne
> „am construit o platformă", nu „am analizat factorii".

```
FOAIE DE TITLU
REZUMAT
ABSTRACT
CUPRINS
ABREVIERI ȘI DEFINIȚII
INTRODUCERE

1 FUNDAMENTAREA TEORETICĂ A FRAUDEI ACADEMICE ÎN EVALUĂRILE ONLINE
  1.1 Evaluarea online: evoluție, forme și vulnerabilități specifice
  1.2 Frauda academică: definire, tipologii și amploarea fenomenului în mediul online
      1.2.1 Factori motivaționali și contextuali ai fraudei academice
  1.3 Indicatori comportamentali asociați fraudei în evaluările digitale
  1.4 Metode și tehnologii de supraveghere a evaluărilor online
      1.4.1 Proctoring uman, automat și hibrid
      1.4.2 Supravegherea video și biometrică
      1.4.3 Analiza telemetriei comportamentale din browser
  1.5 Aspecte etice și juridice ale colectării datelor comportamentale
      1.5.1 Legea nr. 133/2011 privind protecția datelor cu caracter personal
      1.5.2 Principii GDPR aplicabile și consimțământul informat

2 ANALIZA SOLUȚIILOR EXISTENTE ȘI DEFINIREA CERINȚELOR SISTEMULUI
  2.1 Analiza comparativă a platformelor comerciale de proctoring
  2.2 Analiza cercetărilor privind detecția comportamentală a fraudei academice
  2.3 Identificarea limitărilor și lacunelor soluțiilor existente
  2.4 Formularea problemei de cercetare, a scopului și a obiectivelor
  2.5 Selecția și justificarea indicatorilor comportamentali observabili
  2.6 Cerințele funcționale și non-funcționale ale sistemului propus
  2.7 Analiza tehnologiilor și a arhitecturii pentru sistemul propus

3 PROIECTAREA ȘI IMPLEMENTAREA SISTEMULUI DE COLECTARE A DATELOR COMPORTAMENTALE
  3.1 Arhitectura generală a sistemului
  3.2 Modelul de date și taxonomia evenimentelor comportamentale
  3.3 Modulul de colectare a telemetriei în interfața de examinare
      3.3.1 Categoriile de evenimente colectate
      3.3.2 Mecanismul de tamponare și transmitere în loturi
  3.4 Asigurarea integrității și autenticității datelor colectate
      3.4.1 Semnarea criptografică a evenimentelor (HMAC)
      3.4.2 Protecția anti-reluare: nonce, numere de secvență, toleranță de ceas
      3.4.3 Detectarea lacunelor de secvență și a perioadelor de tăcere
  3.5 Procesarea asincronă a fluxului de evenimente (pipeline Kafka → Worker)
  3.6 Modulul de agregare a caracteristicilor și scorare a riscului
  3.7 Interfața de monitorizare în timp real și auditul sesiunilor
  3.8 Mecanismul de etichetare a sesiunilor pentru cercetare
      3.8.1 Ecranul de consimțământ informat
      3.8.2 Auto-raportarea participantului
      3.8.3 Verdictul evaluatorului
  3.9 Tehnologiile utilizate și justificarea alegerii acestora
  3.10 Testarea funcțională și evaluarea capacității sistemului sub sarcină

4 COLECTAREA DATELOR ȘI ANALIZA FACTORILOR ASOCIAȚI FRAUDEI ACADEMICE
  4.1 Metodologia experimentului de colectare a datelor reale
      4.1.1 Protocolul de desfășurare a sesiunilor de evaluare
      4.1.2 Lotul de participanți și considerentele etice
      4.1.3 Procedura de etichetare a sesiunilor
  4.2 Descrierea setului de date colectat
  4.3 Construcția variabilelor explicative (feature engineering)
      4.3.1 Variabile de frecvență
      4.3.2 Variabile temporale și de secvență
      4.3.3 Variabile derivate la nivel de întrebare
  4.4 Analiza exploratorie a datelor
  4.5 Analiza factorilor asociați fraudei academice
      4.5.1 Analiza statistică descriptivă pe grupuri
      4.5.2 Analiza corelațională și testarea semnificației
      4.5.3 Ierarhizarea factorilor după puterea discriminativă
  4.6 Modelarea pentru identificarea comportamentelor suspecte
      4.6.1 Modelele de clasificare aplicate
      4.6.2 Strategia de antrenare și validare
      4.6.3 Metricile de evaluare în condiții de clase dezechilibrate
  4.7 Compararea modelului obținut cu scorarea bazată pe reguli
  4.8 Interpretarea rezultatelor și implicațiile practice
  4.9 Validitatea cercetării și limitările studiului

CONCLUZII
BIBLIOGRAFIE
ANEXE
  ANEXA A. Taxonomia completă a evenimentelor comportamentale colectate
  ANEXA B. Textul consimțământului informat prezentat participanților
  ANEXA C. Instrumentul de evaluare utilizat în experiment
  ANEXA D. Fragmente de cod relevante
```

### 2.1 Stadiul actual: ce este deja acoperit de prototip

| Secțiune | Stare | Acoperire în sistemul existent |
|---|---|---|
| 3.1 Arhitectura | **gata** | React+TS / ASP.NET Core API / Worker / Kafka(Redpanda) / Postgres / Redis / Traefik |
| 3.2 Model de date | **gata** | `behavioral_events`, `session_feature_aggregates`, `risk_score_snapshots`, `exam_sessions` |
| 3.3 Colectare telemetrie | **gata** | ~30 tipuri de evenimente (focus/blur, clipboard, fullscreen, devtools, idle, typing speed, navigare etc.) |
| 3.4 Integritate date | **gata** | semnare HMAC per eveniment, nonce store, validare secvențe, toleranță de ceas |
| 3.5 Pipeline asincron | **gata** | API → Kafka → Worker → Postgres |
| 3.6 Agregare + scorare | **parțial** | agregare gata; scorarea e *rule-based cu ponderi alese manual* (placeholder) |
| 3.7 Dashboard | **gata** | SSE live, timeline sesiune, scorare manuală, export CSV |
| 3.8 Etichetare | **gata** | consimțământ + auto-raportare („Ai trișat?") + verdict evaluator |
| 3.9 Tehnologii | **gata** | de documentat |
| 3.10 Testare sub sarcină | **gata** | teste 100–500 utilizatori concurenți, prin tunel public |
| **Cap. 4 integral** | **de realizat** | necesită examene reale cu studenți + analiza propriu-zisă |

> **Riscul principal al lucrării:** capitolul 4 depinde integral de date reale etichetate.
> Fără o campanie de colectare cu participanți reali, capitolul rămâne nerealizabil, iar titlul
> nu poate fi onorat. Aceasta este prioritatea imediată.

---

## 3. Structura propusă pentru PRACTICA DE CERCETARE

Practica de cercetare precedă teza și are rolul de a fundamenta cercetarea: studiul domeniului,
delimitarea problemei și pregătirea instrumentului de colectare a datelor. Se mapează natural pe
**capitolele 1–3** ale tezei, plus **planul metodologic** pentru capitolul 4.

Avantajul tău: prototipul este deja construit și testat, deci Practica de cercetare poate fi
livrată cu rezultate concrete, nu doar cu o analiză bibliografică.

```
FOAIE DE TITLU
CUPRINS
ABREVIERI ȘI DEFINIȚII
INTRODUCERE
  — actualitatea temei
  — scopul și obiectivele practicii de cercetare
  — metodologia de lucru

1 STUDIUL DOMENIULUI ȘI ANALIZA SURSELOR BIBLIOGRAFICE
  1.1 Frauda academică în evaluările online: amploare și tipologii
  1.2 Indicatori comportamentali asociați fraudei
  1.3 Abordări existente de detecție: proctoring video, biometric și comportamental
  1.4 Analiza critică a soluțiilor existente și identificarea lacunelor
  1.5 Cadrul etic și juridic al colectării datelor comportamentale

2 FORMULAREA PROBLEMEI DE CERCETARE
  2.1 Problema identificată și justificarea abordării propuse
  2.2 Scopul, obiectivele și întrebările de cercetare
  2.3 Selecția și justificarea indicatorilor comportamentali observabili
  2.4 Cerințele instrumentului de colectare a datelor

3 PROIECTAREA ȘI REALIZAREA INSTRUMENTULUI DE COLECTARE A DATELOR
  3.1 Arhitectura sistemului propus
  3.2 Taxonomia evenimentelor comportamentale colectate
  3.3 Mecanismele de asigurare a integrității datelor
  3.4 Mecanismul de etichetare a sesiunilor (consimțământ, auto-raportare, verdict)
  3.5 Tehnologiile utilizate
  3.6 Validarea funcțională și testarea capacității sistemului
      3.6.1 Scenariile de testare funcțională
      3.6.2 Evaluarea comportamentului sub sarcină concurentă
      3.6.3 Rezultatele obținute și interpretarea acestora

4 METODOLOGIA CERCETĂRII PENTRU ETAPA URMĂTOARE
  4.1 Protocolul campaniei de colectare a datelor reale
  4.2 Procedura de etichetare și asigurarea calității etichetelor
  4.3 Planul de construcție a variabilelor explicative
  4.4 Metodele de analiză statistică și de modelare preconizate
  4.5 Criteriile de evaluare a rezultatelor
  4.6 Riscurile identificate și măsurile de atenuare

CONCLUZII
BIBLIOGRAFIE
ANEXE
```

### 3.1 Corespondența Practica de cercetare ↔ Teza finală

| Practica de cercetare | Se extinde în teză în |
|---|---|
| Cap. 1 (studiul domeniului) | Cap. 1 integral + 2.1–2.3 |
| Cap. 2 (problema de cercetare) | 2.4–2.6 |
| Cap. 3 (instrumentul realizat) | Cap. 3 integral |
| Cap. 4 (metodologia pentru etapa următoare) | 4.1 (devine metodologie *aplicată*, nu *planificată*) |
| — | **Cap. 4.2–4.9 este contribuția nouă a tezei** |

Astfel, Practica de cercetare nu se irosește: ~60% din conținutul ei migrează direct în teză,
iar teza adaugă deasupra componenta analitică (datele reale + analiza factorilor + modelarea).

---

## 4. Recomandări

1. **Verifică structura cu coordonatorul înainte de a scrie.** Documentul acesta se bazează pe
   tipare observate în teze deja susținute, nu pe un ghid metodologic oficial al departamentului —
   dacă există un astfel de ghid, el are prioritate.

2. **Prioritatea imediată este campania de colectare a datelor reale.** Capitolele 1–3 pot fi
   scrise oricând; capitolul 4 nu există fără date. Fiecare săptămână fără sesiuni reale cu
   studenți este timp nerecuperabil pentru partea care contează academic cel mai mult.

3. **Tratează eticheta auto-raportată ca semnal zgomotos, nu ca adevăr absolut.** Un participant
   care a fraudat poate răspunde „Nu". Secțiunea 4.9 (limitările studiului) trebuie să discute
   explicit acest lucru — e un punct de onestitate metodologică apreciat la evaluare.

4. **Compararea model antrenat vs. scorare bazată pe reguli (secțiunea 4.7) este cel mai puternic
   argument de contribuție proprie.** Ponderile actuale din `appsettings.json` sunt alese manual,
   arbitrar — demonstrarea că un model derivat din date le depășește reprezintă rezultatul
   științific central al lucrării.

5. **Include în bibliografie publicații ale cadrelor didactice UTM** relevante pentru securitate,
   analiza datelor și e-learning — este un tipar constant în tezele analizate.
