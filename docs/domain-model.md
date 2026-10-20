Modello di dominio di MiddiFranchi
Stato: specifica concettuale per l'implementazione futura. Non contiene codice applicativo, classi concrete, dipendenze o migrazioni.
1. Contesto e confini architetturali
MiddiFranchi è un'applicazione personale per registrare entrate, spese, trasferimenti, conti e investimenti, con ricostruzione storica del patrimonio. La soluzione segue la dipendenza Domain → Application → Infrastructure → Web:
- Domain: regole, invarianti, concetti finanziari e operazioni di business. Non dipende da EF Core, PostgreSQL, HTTP, Blazor o provider esterni.
- Application: casi d'uso, coordinamento delle operazioni, query, importazioni e orchestrazione delle fonti di prezzo e cambio.
- Infrastructure: persistenza e integrazioni tecniche, incluse le implementazioni dei provider.
- Web: interfaccia utente.
Gli importi, i prezzi, i cambi e le quantità sono calcolati con decimal, mai con tipi in virgola mobile. L'arrotondamento è esplicito e avviene solo quando richiesto dalla valuta, dalla banca/broker o dalla presentazione.

Perimetro della prima versione:
- conti in più valute, entrate, spese e trasferimenti;
- portafogli e strumenti finanziari;
- acquisti, vendite parziali, dividendi, cedole e commissioni;
- guadagni e perdite realizzati e non realizzati;
- valutazione storica di liquidità, quantità, posizioni e patrimonio;
- rendicontazione in EUR, mantenendo i valori originari e i cambi storici;
- costo medio ponderato a fini di reportistica personale, non fiscale;
- registrazioni append-only con storni e correzioni retrodatate.
Sono rinviati: TWR, XIRR, motore fiscale, importazione CSV, ricostruzione bitemporale, decomposizione completa dell'effetto cambio, ratei obbligazionari e trasferimenti di titoli tra portafogli.
2. Concetti e relazioni
Account
└── AccountPosting

Category

FinancialOperation
├── Income
├── Expense
├── Transfer
├── InvestmentOperation
└── ReversalOperation

Portfolio
├── valuta di riferimento per la presentazione
└── conti di regolamento ammessi

Instrument
├── valuta di quotazione
├── unità di prezzo
├── caratteristiche specifiche dello strumento
└── ValuationPolicy

PriceObservation (serie storica append-only per strumento)
ExchangeRateObservation (serie storica append-only rispetto a EUR)
I nomi in questo documento identificano concetti di dominio, non implicano che ciascuno debba diventare una classe separata. FeeComponent e QuantityComponent sono componenti di un'operazione e non entità autonome.

Ogni dato registrato ha una fonte di verità. Saldi, quantità, costo residuo, prezzi applicabili, valori di mercato e risultati sono derivati dalle operazioni e dalle osservazioni; non sono campi modificabili che sostituiscono la storia.
2.1 Account
Un Account rappresenta un conto corrente, conto titoli con liquidità, carta, cassa o passività monetaria. Conserva almeno identificativo, nome, tipo, valuta e stato.

Il saldo a una data economica T è la somma dei SignedAmount dei posting validi associati al conto, con OperationDate ≤ T, secondo la politica degli storni descritta nella sezione 5. Il saldo è sempre nella valuta del conto. Un saldo iniziale è registrato come operazione esplicita, non salvato come valore modificabile separato.

Un conto di regolamento rappresenta esclusivamente liquidità; non conserva quantità di strumenti finanziari.
2.2 Portfolio
Un Portfolio raggruppa posizioni e definisce i conti di regolamento ammessi. Può avere una valuta di riferimento per la presentazione aggregata, ma questa non cambia la valuta originaria delle operazioni o degli strumenti.

La posizione è identificata dalla coppia (Portfolio, Instrument). La quantità detenuta e il costo residuo sono ricostruiti dagli eventi d'investimento validi.
2.3 Instrument
Un Instrument è una voce di catalogo riutilizzabile da più portafogli. Conserva identificativo, nome, tipo, valuta di quotazione, unità di prezzo e politica di valutazione. Per le obbligazioni può inoltre conservare valore nominale per unità, tasso cedolare, frequenza delle cedole, scadenza ed emittente.

La valuta di quotazione è la valuta dei prezzi di mercato e del costo medio ponderato. Non deve essere confusa con la valuta del conto che regola l'acquisto o la vendita.
2.4 Category
Una Category classifica esclusivamente entrate e spese ordinarie. Ha nome, tipo (Income oppure Expense), descrizione opzionale e stato. Non classifica trasferimenti o operazioni d'investimento.
3. FinancialOperation e registrazioni monetarie
3.1 Dati comuni
FinancialOperation è il concetto generale di operazione finanziaria. Le specializzazioni previste sono Income, Expense, Transfer, InvestmentOperation e ReversalOperation.

Dati comuni:
- Id;
- OperationDate (DateOnly): data economica usata nelle ricostruzioni;
- RecordedAt (istante UTC): momento in cui la registrazione è stata effettuata, utile per audit;
- Description e Notes opzionali;
- stato di registrazione, ad esempio Registered o Reversed;
- tipo semantico dell'operazione.
OperationDate e RecordedAt hanno significati diversi: la prima determina a quale data economica l'operazione contribuisce; la seconda documenta quando l'applicazione ne è venuta a conoscenza.
3.2 Income ed Expense
Income ed Expense contengono conto, categoria, importo positivo e valuta della componente. La valuta deve essere compatibile con il conto oppure deve essere esplicitata la conversione e il valore effettivamente regolato.
- Income produce un posting positivo sul conto.
- Expense produce un posting negativo sul conto.
Non si memorizzano importi negativi nelle proprietà di importo dell'operazione: il segno compare una sola volta nel posting.
3.3 Transfer
Un Transfer sposta liquidità tra un conto sorgente e un conto destinazione. Registra gli importi positivi dei due lati, ciascuno nella valuta del proprio conto, e conserva il cambio effettivamente applicato quando le valute differiscono.

Un trasferimento produce almeno due posting di capitale: addebito sul conto sorgente e accredito sul conto destinazione. Eventuali commissioni sono componenti esplicite, con un posting negativo sul conto che le sostiene. Il trasferimento non è un'entrata o una spesa e non deve alterare di per sé il patrimonio consolidato.

Quando i conti hanno valute diverse, la somma numerica dei posting non ha significato. La riconciliazione si effettua usando gli importi originari, il cambio applicato e, per i report in EUR, una conversione documentata. La prima versione non genera automaticamente utili o perdite di cambio sui trasferimenti.
3.4 InvestmentOperation
Un'operazione d'investimento è collegata a un portafoglio e a uno strumento. Quando comporta liquidità, è collegata anche a un conto di regolamento. La valuta del conto, quella della componente e quella di quotazione dello strumento possono essere diverse e vanno conservate separatamente.

Tipi inclusi nella prima versione:
- Buy: acquisto di quantità positiva a prezzo unitario positivo; genera un addebito di capitale e gli eventuali addebiti delle commissioni.
- Sell: vendita di quantità positiva non superiore alla quantità detenuta alla data economica; genera un accredito di capitale e gli eventuali addebiti delle commissioni.
- Dividend: provento da dividendo accreditato al conto; non modifica la quantità.
- Coupon: provento da cedola accreditato al conto; non modifica la quantità.
- StandaloneFee: commissione autonoma collegata al portafoglio e, quando applicabile, allo strumento; addebita un conto ma non modifica quantità e costo base.
TransferIn, TransferOut e QuantityEvent sono concetti futuri. Non devono essere simulati come acquisti o vendite: un trasferimento di titoli deve preservare il costo storico e non generare un risultato realizzato.

Gli importi lordi, le commissioni, le eventuali ritenute fiscali e gli importi effettivamente regolati vanno distinti. Una ritenuta (TaxWithheld) non è una commissione: il trattamento fiscale completo è fuori dalla prima versione, ma i dati lordi e la ritenuta non devono essere confusi o persi.
3.5 AccountPosting
Ogni componente monetaria che modifica un conto produce un AccountPosting. Il posting conserva almeno:
- identificativo dell'operazione;
- conto;
- tipo di posting (Capital, Fee, Income, TransferLeg o equivalente);
- valuta del conto;
- SignedAmount, nella valuta del conto;
- riferimento alla componente originaria e, se necessario, cambio applicato e importo effettivamente regolato.
Un posting non duplica la fonte di verità: è la rappresentazione contabile del movimento che incide sul saldo. Ogni componente monetaria deve incidere sul conto una sola volta. Per un'operazione multivaluta il dato originario e il controvalore regolato devono essere entrambi ricostruibili.
4. Segni, valute e cambi
4.1 Convenzione dei segni
Le magnitudini di importi, prezzi, commissioni e quantità sono positive. Il segno monetario viene applicato una sola volta, nei posting, in base alla semantica dell'operazione.
Operazione	Componente	Effetto sul conto
Expense	importo	negativo
Income	importo	positivo
Transfer	lato sorgente	negativo
Transfer	lato destinazione	positivo
Buy	capitale	negativo
Buy	commissione d'acquisto	negativo
Sell	capitale lordo	positivo
Sell	commissione di vendita	negativo
Dividend / Coupon	provento accreditato	positivo
StandaloneFee	commissione	negativo


Le commissioni sono registrate separatamente dal capitale, anche quando il broker le addebita nello stesso regolamento. Il saldo del conto somma i posting effettivi; non si riapplica il segno a valle.
4.2 Le quattro valute da distinguere
1. Valuta del conto: valuta in cui è espresso il saldo e il SignedAmount del posting.
2. Valuta originaria della componente: valuta in cui è espresso l'importo comunicato dalla banca o dal broker.
3. Valuta di quotazione dello strumento: valuta di prezzo e costo medio della posizione.
4. Valuta di rendicontazione: EUR nella prima versione; è una scelta applicativa per i report.
Se una commissione è comunicata in USD ma addebitata su un conto EUR, vanno conservati importo originario in USD, valuta, cambio applicato e importo effettivamente addebitato in EUR. Se la banca comunica l'importo effettivo, esso prevale sul controvalore teorico calcolato.
4.3 Convenzione di cambio
Nella prima versione ogni osservazione di cambio è espressa rispetto a EUR: Rate indica quante unità della valuta estera equivalgono a 1 EUR. Per esempio, USD Rate = 1,10 significa 1 EUR = 1,10 USD.
- Da valuta estera a EUR: importo / Rate.
- Da EUR a valuta estera: importo × Rate.
Si distinguono sempre:
- cambio applicato: cambio effettivamente utilizzato nella transazione, conservato con la registrazione e mai ricalcolato usando dati correnti;
- cambio di valutazione: osservazione storica valida alla data di valutazione, usata per convertire il patrimonio a quella data.
Se manca un cambio applicato ma serve un controvalore in EUR per una metrica storica, si usa l'osservazione valida alla data economica, se disponibile, e si dichiara la fonte. Se manca anche questa osservazione, il controvalore è non valutato: non si inventa un tasso.

Il modello a coppie arbitrarie di valute è rinviato. Non si deve applicare una conversione implicita due volte né trattare un importo già regolato nella valuta del conto come se fosse ancora nella valuta originaria.
5. Storni, correzioni e storia
La prima versione usa una strategia append-only con correzioni esplicite:
1. Un'operazione registrata non viene cancellata fisicamente né riscritta silenziosamente.
2. Uno storno è una ReversalOperation che identifica l'operazione originale e conserva il proprio momento di registrazione. Non crea un secondo movimento monetario o di quantità.
3. L'operazione originale resta consultabile ma viene esclusa dalle ricostruzioni economiche aggiornate quando è marcata come stornata.
4. La correzione è una nuova operazione, con i propri componenti e posting. Può avere OperationDate retrodatata, mentre RecordedAt indica quando la correzione è stata inserita.
5. La vista economica aggiornata a T include le operazioni attualmente valide con OperationDate ≤ T, indipendentemente da RecordedAt. Di conseguenza una correzione inserita oggi può cambiare i risultati ricostruiti di date passate: è il comportamento intenzionale della prima versione.
6. La domanda «che cosa risultava noto all'applicazione in una data passata?» richiede una vista bitemporale, futura. RecordedAt e la traccia degli storni vengono conservati per consentire tale evoluzione.
Una ReversalOperation non deve essere interpretata come un posting negativo che si somma al posting originale mentre l'originale rimane incluso. In questa strategia l'originale viene escluso dalla vista economica corrente e la correzione, se presente, entra una sola volta.
6. Prezzi, obbligazioni e valutazioni
6.1 PriceObservation
Una PriceObservation conserva strumento, prezzo positivo, unità di prezzo, ValidAt (quando il prezzo è valido), ObservedAt (quando è stato acquisito) e fonte. Le osservazioni sono append-only: un nuovo prezzo non sovrascrive quello precedente.

Per la valutazione alla data T si sceglie l'osservazione con ValidAt ≤ T più recente tra quelle ammissibili. Se l'età supera la soglia di obsolescenza configurata, la posizione è segnalata come non valutata o con prezzo obsoleto secondo una politica esplicita; non si presenta un prezzo vecchio come corrente senza indicazione. Non si usano mai prezzi futuri rispetto a T.

ValuationPolicy descrive se lo strumento è valutato manualmente, tramite provider o con valore fisso, oltre alla soglia di obsolescenza. Un valore fisso è una politica esplicita, non una scorciatoia che elimina la provenienza del valore.
6.2 PriceUnit e obbligazioni
- PerUnit: prezzo espresso nella valuta di quotazione per una unità dello strumento. MarketValue = Quantity × Price.
- PercentOfNominal: prezzo espresso come percentuale del valore nominale per unità. EffectiveUnitPrice = NotionalValue × Price / 100; MarketValue = Quantity × EffectiveUnitPrice.
Esempio: nominale di 1.000 EUR, prezzo al 98,5%: prezzo unitario effettivo 985 EUR; per 3 unità, valore di mercato 2.955 EUR.

Il valore nominale è una caratteristica dello strumento, non un prezzo di mercato. I ratei maturati non sono inclusi nel valore di mercato definito qui: quando saranno implementati, il valore comprensivo di ratei sarà una metrica distinta. Le cedole sono proventi registrati come Coupon; non si incorporano nel prezzo. Le commissioni restano componenti separate.
6.3 ExchangeRateObservation
Un'osservazione di cambio conserva valuta estera, tasso rispetto a EUR, ValidAt, ObservedAt e fonte. Le osservazioni sono append-only. Una valutazione a T può usare soltanto il cambio più recente con ValidAt ≤ T che rispetti la politica di validità. Se il cambio necessario manca, il valore convertito è dichiarato incompleto.
7. Costo base e risultati d'investimento
7.1 Convenzioni contabili
Le metriche di investimento sono distinte dal patrimonio netto:
- NetWorth(T): saldi della liquidità più valore di mercato delle posizioni alla data T, convertiti in EUR. È una fotografia patrimoniale.
- Risultato economico cumulato: guadagni/perdite realizzati, guadagni/perdite non realizzati, proventi e commissioni autonome, senza sommare nuovamente il saldo di cassa.
- Risultato di periodo: variazione del risultato cumulato tra due date, con trattamento coerente delle variazioni di valutazione e dei flussi.
Non si deve sommare NetWorth al risultato economico: sono misure diverse. In particolare, un dividendo aumenta la liquidità e contribuisce al risultato economico come provento, ma non va aggiunto due volte nella stessa metrica.
7.2 Costo medio ponderato
Il costo medio ponderato è una convenzione di reportistica personale, non un metodo fiscale.

Per ogni acquisto valido:
AcquisitionCostQuote = Quantity × UnitPrice + BuyFeesInQuoteCurrency
AverageCost = TotalRemainingCostInQuoteCurrency / QuantityHeld
Le commissioni d'acquisto incluse nel costo base devono essere convertite nella valuta di quotazione con il cambio applicato e i dati effettivi disponibili. Se non è possibile determinarne il controvalore con dati affidabili, il costo base in valuta di quotazione è incompleto: non si assume implicitamente un cambio.

Una vendita riduce quantità e costo base in proporzione al costo medio immediatamente precedente alla vendita. Il costo unitario medio in valuta di quotazione non cambia a seguito della vendita stessa; cambia con nuovi acquisti o altri eventi di quantità esplicitamente definiti.

Il costo storico in EUR è calcolato separatamente dagli eventi di acquisto residui, usando il controvalore storico effettivo di ogni evento e il relativo cambio applicato. Non si ottiene convertendo il costo medio aggregato in valuta di quotazione con il cambio corrente.

Per mantenere coerenza tra valuta di quotazione ed EUR, le vendite devono consumare pro-rata i lotti di acquisto residui ai fini del costo storico EUR, pur applicando il costo medio ponderato per la metrica in valuta di quotazione. La regola di allocazione pro-rata è una convenzione di reportistica; non pretende di essere un metodo fiscale.
7.3 Formule in valuta di quotazione
Per una posizione (Portfolio, Instrument) alla data T:
QuantityHeld(T) = somma delle quantità firmate degli eventi validi fino a T
CostBasis(T) = costo base residuo, incluse le commissioni di acquisto
AverageCost(T) = CostBasis(T) / QuantityHeld(T), se QuantityHeld(T) > 0
PositionValue(T) = QuantityHeld(T) × prezzo unitario applicabile a T
UnrealizedPL(T) = PositionValue(T) − CostBasis(T)
Per ogni vendita:
NetSaleProceeds = GrossSaleProceeds − SellFees
RealizedPLForSale = NetSaleProceeds − CostBasisAllocatedToSoldQuantity
CostBasisAllocatedToSoldQuantity usa il costo medio ponderato immediatamente prima della vendita. Se il prezzo è espresso come percentuale del nominale, il prezzo unitario effettivo viene calcolato prima di applicare la formula.

I proventi cumulati (Income) sono la somma dei dividendi e delle cedole validi, convertiti in EUR con il cambio applicato quando disponibile; in sua assenza si usa il cambio storico valido alla data dell'operazione, se disponibile. Le commissioni autonome (OtherFees) sono sommate separatamente. Le commissioni d'acquisto sono già incluse nel costo base e quelle di vendita già incluse nei proventi netti: nessuna delle due viene sottratta una seconda volta.
TotalResult(T) = RealizedPL(T)
              + UnrealizedPL(T)
              + Income(T)
              − OtherFees(T)
Questa formula rappresenta il risultato economico cumulato del perimetro d'investimento, in una valuta di rendicontazione coerente. Se la posizione non è valutabile per mancanza di prezzo o cambio, il risultato aggregato deve essere marcato incompleto, non calcolato fingendo che il valore mancante sia zero.

Per un intervallo [T1, T2]:
PeriodResult = TotalResult(T2) − TotalResult(T1)
La differenza dei risultati cumulati è la definizione di riferimento. Eventuali viste componenti di periodo devono essere coerenti con questa differenza, inclusa la variazione dell'utile/perdita non realizzato. Questa misura non è TWR o XIRR e non va presentata come rendimento percentuale.
7.4 Risultati in EUR e effetto cambio
I risultati in valuta di quotazione e quelli in EUR non sono intercambiabili. Per la rendicontazione EUR:
- i costi storici residui sono derivati dagli importi effettivi e dai cambi storici degli acquisti;
- i proventi di vendita sono convertiti usando il cambio effettivo applicato alla vendita, quando disponibile;
- il valore di mercato è convertito usando il cambio di valutazione valido alla data richiesta;
- i dividendi, le cedole e le commissioni usano il cambio applicato o, se manca, il cambio storico valido alla data dell'operazione;
- le differenze dovute a prezzo e cambio non vengono decomposte nella prima versione.
Le conversioni devono essere coerenti con la valuta in cui è espresso ciascun dato. Non è corretto sottrarre un costo in USD da un ricavo in EUR o sommare importi di valute diverse senza conversione esplicita.
8. Esempi numerici di verifica
Gli esempi usano importi decimal. Gli arrotondamenti sono mostrati solo per leggibilità.
8.1 Acquisto e rivalutazione
Acquisto di 2 unità a 100 EUR, senza commissioni: costo base 200 EUR. Alla data di valutazione il prezzo è 110 EUR: valore di mercato 220 EUR, UnrealizedPL = +20 EUR, RealizedPL = 0, proventi e commissioni autonome pari a zero. TotalResult = +20 EUR.
8.2 Acquisto con commissione e vendita parziale
Acquisto di 2,5 quote a 80 EUR con commissione di acquisto 1,50 EUR:
- costo base = 2,5 × 80 + 1,50 = 201,50 EUR;
- costo medio = 201,50 / 2,5 = 80,60 EUR.
Vendita di 1 quota a 95 EUR con commissione di vendita 1 EUR:
- provento netto = 95 − 1 = 94 EUR;
- costo allocato = 80,60 EUR;
- risultato realizzato = 94 − 80,60 = +13,40 EUR;
- quantità residua = 1,5; costo residuo = 120,90 EUR.
Con prezzo successivo pari a 90 EUR, il valore residuo è 135 EUR e l'utile non realizzato è 14,10 EUR. Risultato totale cumulato = 13,40 + 14,10 = +27,50 EUR. La verifica patrimoniale è −201,50 + 94 + 135 = +27,50 EUR.
8.3 Due acquisti in USD con cambi diversi
Strumento quotato in USD, cambi espressi in USD per EUR:
- acquisto A: 100 unità a 10 USD, commissione 10 USD, cambio 1,01. Costo totale 1.010 USD, costo storico 1.000 EUR;
- acquisto B: 100 unità a 12 USD, nessuna commissione, cambio 1,20. Costo totale 1.200 USD, costo storico 1.000 EUR.
Dopo entrambi gli acquisti, quantità 200, costo base 2.210 USD, costo medio 11,05 USD, costo storico complessivo 2.000 EUR.

Vendita di 50 unità a 13,20 USD, senza commissioni, cambio applicato 1,10:
- provento netto = 660 USD = 600 EUR;
- costo medio allocato in USD = 50 × 11,05 = 552,50 USD;
- risultato realizzato in USD = 107,50 USD;
- per la rendicontazione storica EUR, con allocazione pro-rata dei due acquisti, il costo storico delle 50 unità vendute è 500 EUR e il risultato realizzato è 600 − 500 = +100 EUR.
Restano 150 unità, costo base 1.657,50 USD e costo storico residuo 1.500 EUR. Con prezzo di mercato 14 USD e cambio di valutazione 1,10, il valore di mercato è 2.100 USD, cioè circa 1.909,09 EUR. L'utile non realizzato in EUR è circa 409,09 EUR. L'effetto cambio non è decomposto nella prima versione.
8.4 Dividendo o cedola in valuta estera
Un provento di 12,50 USD è accreditato su un conto USD. Il cambio applicato è 1,25 USD per EUR: il provento rendicontato è 10 EUR. Il saldo USD aumenta di 12,50 USD e il risultato economico include il provento convertito una sola volta. Il NetWorth usa invece il cambio di valutazione valido alla data richiesta.
8.5 Commissione autonoma
Una StandaloneFee di 0,50 EUR riduce il conto di 0,50 EUR e viene sottratta dal risultato economico una volta. Non modifica quantità o costo base della posizione.
8.6 Storno e correzione
Un acquisto registrato per 2,5 quote viene stornato e sostituito con una nuova operazione corretta da 1,5 quote, con data economica originale e RecordedAt attuale. La ricostruzione economica aggiornata esclude l'acquisto stornato e include quello corretto. Non si crea un posting compensativo dello storno oltre alla sostituzione: il saldo aggiornato riflette solo l'operazione valida. La vista storica «come noto allora» resta futura.
8.7 Vendita eccedente
Se la posizione contiene 1 quota e viene tentata la vendita di 2 quote, l'operazione è rifiutata e non produce posting o variazioni di quantità. Le vendite allo scoperto non sono ammesse nella prima versione.
9. Invarianti di dominio
1. Gli importi, i prezzi, le commissioni e le quantità inseriti come magnitudini sono positivi; i valori zero sono ammessi solo dove una regola specifica lo consente.
2. Il segno monetario viene applicato una sola volta nei posting.
3. Ogni componente monetaria produce un effetto sul conto una sola volta; ogni posting appartiene a un'operazione e a un conto.
4. I posting sono espressi nella valuta del conto; importo originario, valuta originaria, cambio applicato e importo regolato sono conservati quando differiscono.
5. Buy e Sell non sono entrate/spese ordinarie. Dividendi e cedole non sono Income ordinari. I trasferimenti non sono entrate o spese.
6. Una vendita è valida solo se la quantità venduta non supera quella detenuta alla data economica, secondo l'insieme delle operazioni valide.
7. Le operazioni stornate sono escluse dalla ricostruzione economica aggiornata; storni e correzioni restano tracciabili.
8. Una valutazione storica usa solo osservazioni con ValidAt ≤ T. Prezzi e cambi mancanti o obsoleti generano uno stato di valutazione incompleta, non un valore inventato.
9. Prezzi, saldi, quantità, costi e risultati sono derivati dalle registrazioni; eventuali cache o proiezioni future devono poter essere ricostruite e riconciliate.
10. Il Domain non dipende da database, provider, protocolli HTTP o framework UI.
11. Le conversioni monetarie esplicitano valuta, direzione del cambio e fonte. Non si sommano importi di valute diverse senza conversione.
12. Commissioni d'acquisto e vendita non vengono sottratte due volte; le commissioni autonome sono costi separati.
13. I valori derivati non valutabili propagano uno stato di incompletezza nei risultati aggregati.
10. Prima versione e sviluppi futuri
10.1 Inclusi nella prima versione
- Account, AccountPosting, Category, Portfolio, Instrument;
- FinancialOperation con Income, Expense, Transfer, InvestmentOperation e ReversalOperation;
- componenti monetarie e di quantità, valute, prezzi e cambi;
- operazioni di acquisto, vendita parziale, dividendo, cedola e commissione autonoma;
- saldi, quantità, costo base, costo medio, valore di posizione e patrimonio netto;
- risultato realizzato, non realizzato, proventi, commissioni autonome e risultato cumulato;
- osservazioni storiche di prezzi e cambi, con controlli su data e obsolescenza;
- storni append-only e correzioni retrodatate nella vista economica aggiornata;
- reportistica in EUR con cambi storici.
10.2 Rinviati
- TWR, XIRR e classificazione definitiva dei flussi esterni;
- motore fiscale e regole fiscali specifiche;
- importazione CSV e deduplicazione tramite identificativi esterni;
- ricostruzione bitemporale «come noto alla data»;
- trasferimenti di titoli tra portafogli, operazioni societarie e altri eventi di quantità;
- ratei obbligazionari e valore comprensivo di ratei;
- provider reali e automazioni di aggiornamento;
- modello di cambio per coppie arbitrarie;
- decomposizione del risultato tra effetto prezzo ed effetto cambio.
I dati originari, le date, i cambi applicati, le osservazioni, gli importi lordi e le ritenute devono essere conservati quando disponibili, così da non precludere gli sviluppi futuri.
11. Decisioni da confermare prima o durante l'implementazione
1. Commissioni d'acquisto: raccomandazione di includerle nel costo base e non sottrarle di nuovo dal risultato.
2. Commissioni di vendita: raccomandazione di sottrarle dai proventi della vendita per calcolare il risultato realizzato.
3. Costo storico EUR: derivarlo dai costi storici dei singoli eventi e dai cambi applicati, non dal cambio corrente.
4. Trasferimenti in valuta estera: nessuna contabilizzazione automatica di utili/perdite di cambio nella prima versione; conservarne però tutti i dati.
5. Prezzi obsoleti: soglia predefinita configurabile in Application, con override per strumento; la posizione deve apparire non valutata o esplicitamente obsoleta oltre soglia.
6. Valuta di rendicontazione: EUR nella prima versione; non altera la valuta originaria delle registrazioni.
7. TWR/XIRR: rimandati finché non è definita la distinzione tra flussi esterni, proventi e trasferimenti interni.
8. Trasferimento titoli: quando implementato, deve trasferire anche il costo storico residuo e non realizzare guadagni o perdite.
9. Ritenute fiscali: conservarle separate dalle commissioni, pur rinviando il calcolo fiscale.
10. Valutazioni incomplete: un report aggregato che dipende da prezzi o cambi mancanti deve dichiarare l'incompletezza invece di sostituire valori mancanti con zero.
Nota di implementazione: prima di introdurre entità e persistenza, le formule della sezione 7 devono essere coperte da test unitari con casi in EUR e in valuta estera. In particolare, il risultato in EUR delle vendite deve avere una convenzione unica e documentata per l'allocazione del costo storico; non si devono mescolare silenziosamente costo medio in valuta di quotazione e costo dei singoli lotti in EUR.