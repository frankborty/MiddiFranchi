Modello di dominio di MiddiFranchi
Stato: specifica concettuale dell'implementazione corrente. Non contiene codice applicativo.

1. Contesto e confini architetturali
MiddiFranchi è un'applicazione personale per registrare entrate, spese, trasferimenti, conti e investimenti, con ricostruzione storica del patrimonio. La soluzione segue la dipendenza Domain → Application → Infrastructure → Web:
- Domain: regole, invarianti, concetti finanziari e operazioni di business. Non dipende da EF Core, PostgreSQL, HTTP, Blazor o provider esterni.
- Application: casi d'uso, coordinamento delle operazioni, query e orchestrazione delle fonti di prezzo.
- Infrastructure: persistenza e integrazioni tecniche.
- Web: interfaccia utente.

Tutti gli importi dell'applicazione sono in EUR. Non esistono altre valute, osservazioni di cambio o conversioni monetarie. Gli importi, i prezzi e le quantità sono calcolati con decimal, mai con tipi in virgola mobile. L'arrotondamento è esplicito e avviene solo per la presentazione o quando richiesto dalla banca/broker.

Perimetro della prima versione:
- conti in EUR, entrate, spese e trasferimenti;
- strumenti finanziari e operazioni d'investimento;
- acquisti, vendite parziali, dividendi, cedole e commissioni;
- guadagni e perdite realizzati e non realizzati;
- valutazione storica di liquidità, quantità, posizioni e patrimonio;
- costo medio ponderato a fini di reportistica personale, non fiscale;
- registrazioni append-only con storni e correzioni retrodatate.
Sono rinviati: TWR, XIRR, motore fiscale, importazione CSV, ricostruzione bitemporale, ratei obbligazionari e trasferimenti di titoli.

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

Instrument
├── prezzi in EUR
├── unità di prezzo
├── caratteristiche specifiche dello strumento
└── ValuationPolicy

PriceObservation (serie storica append-only per strumento)

I nomi in questo documento identificano concetti di dominio, non implicano che ciascuno debba diventare una classe separata. FeeComponent e QuantityComponent sono componenti di un'operazione e non entità autonome.

Ogni dato registrato ha una fonte di verità. Saldi, quantità, costo residuo, prezzi applicabili, valori di mercato e risultati sono derivati dalle operazioni e dalle osservazioni; non sono campi modificabili che sostituiscono la storia.

2.1 Account
Un Account rappresenta un conto corrente, conto titoli con liquidità, carta, cassa, passività monetaria o conto prestiti. Conserva almeno identificativo, nome, tipo, stato e valuta EUR. Può inoltre conservare un colore personalizzato nel formato #RRGGBB (usato nei grafici a barre) e un'icona come data URL; entrambi sono facoltativi. Nome, tipo, stato, colore e icona sono modificabili dal dettaglio del conto, mentre la valuta resta immutabile.

L'applicazione garantisce due conti speciali, creati in modo idempotente all'avvio: **Cash** (tipo Cassa) e **Prestiti** (tipo Prestiti). Il conto Cash è un normale conto di cassa; il conto Prestiti aggrega i prestiti concessi.

Per un Account esistono due semantiche di saldo, distinte e non equivalenti:
- Il **saldo ricostruito dai posting** a una data economica T è la somma dei SignedAmount dei posting validi associati al conto, con OperationDate ≤ T, secondo la politica degli storni descritta nella sezione 4. È una grandezza derivata dalle operazioni registrate.
- Il **saldo manuale** è il saldo corrente del conto, determinato dall'ultima rilevazione manuale registrata (sezione 2.1.1). È la fonte autorevole del valore corrente ed è il saldo predefinito per la consultazione del conto. Le entrate, le uscite, i trasferimenti e gli altri posting non lo modificano automaticamente.

I due saldi coesistono senza che l'uno sostituisca l'altro: la registrazione di un movimento aggiorna il saldo ricostruito, mai il saldo manuale; la registrazione di una rilevazione manuale aggiorna il saldo corrente, mai i posting. Un saldo iniziale può essere registrato sia come operazione esplicita sia come rilevazione manuale: sono dati diversi e nessuno dei due è salvato come valore modificabile separato.

Un conto di regolamento rappresenta esclusivamente liquidità; non conserva quantità di strumenti finanziari. Un conto può essere eliminato solo se non ha operazioni registrate; l'eliminazione rimuove anche le sue rilevazioni manuali e, per un conto Prestiti, i prestiti con le loro rilevazioni.

2.1.1 Rilevazione manuale del saldo (ManualBalance)
Una ManualBalance è una rilevazione manuale del saldo assoluto di un conto, registrata dall'utente. Conserva identificativo, conto di riferimento, importo in EUR, data della rilevazione ValidAt (data economica a cui il saldo osservato si riferisce) e istante di registrazione RecordedAt (UTC). ValidAt e RecordedAt hanno ruoli distinti: la prima determina da quale data la rilevazione è consultabile storicamente, la seconda documenta quando è stata inserita e risolve l'ordine tra rilevazioni concorrenti.

Le invarianti della rilevazione manuale:
1. Le rilevazioni sono dati storici: una nuova rilevazione aggiunge una riga; l'utente può correggere importo e data di una rilevazione oppure eliminarla dal dettaglio del conto. RecordedAt resta l'istante della prima registrazione.
2. L'importo è espresso in EUR. Il saldo osservato può essere negativo o zero: nessuna regola di positività si applica a un'osservazione.
3. Il **saldo corrente** del conto è l'ultima rilevazione registrata per il conto, scelta per (RecordedAt, Id) discendenti. L'Application rifiuta le nuove rilevazioni con ValidAt successiva alla data corrente.
4. La **consultazione storica** a una data T restituisce la massima rilevazione con ValidAt ≤ T per (ValidAt, RecordedAt, Id) discendenti.
5. Se non esiste alcuna rilevazione corrente o valida a T, il saldo manuale è esplicitamente non disponibile, mai interpretato silenziosamente come zero.
6. Le registrazioni di entrate, uscite, trasferimenti, commissioni e altri posting non scrivono né aggiornano le rilevazioni manuali e non generano rettifiche contabili o operazioni fittizie.
7. Nessuna vista bitemporale completa è richiesta in questa versione; ValidAt e RecordedAt vengono conservati per consentirla in futuro.
8. L'Application rifiuta una nuova rilevazione con ValidAt successiva alla data corrente, valutata in UTC alla registrazione. Il Domain resta indipendente dall'orologio.

2.1.2 Prestito e rilevazione del prestito (Loan, LoanReading)
Un **Loan** appartiene esclusivamente a un conto di tipo Prestiti e conserva identificativo, debitore, conto di riferimento e istante di creazione. Un **LoanReading** è una rilevazione manuale dell'importo ancora dovuto per quel singolo prestito: conserva importo in EUR, ValidAt e RecordedAt con le stesse regole delle rilevazioni manuali dei conti.

Invarianti dei prestiti:
1. Ogni prestito ha le proprie rilevazioni, indipendenti da quelle degli altri prestiti.
2. Il saldo del conto Prestiti alla data T è la somma dell'ultima rilevazione di ciascun prestito con ValidAt ≤ T; un prestito senza rilevazioni contribuisce con zero.
3. Il saldo aggregato è materializzato come ManualBalance del conto Prestiti a ogni data con rilevazioni e alla data corrente. La materializzazione è una proiezione ricostruita a ogni modifica di prestito o rilevazione e anche all'avvio dell'applicazione, non una seconda fonte di verità.
4. Grafici, dashboard e patrimonio netto leggono il saldo aggregato del conto; non sommano i singoli prestiti separatamente.
5. L'Application rifiuta le rilevazioni future; l'eliminazione di un prestito rimuove le sue rilevazioni e ricostruisce il saldo aggregato.
6. Un prestito è considerato **attivo** finché l'ultima rilevazione disponibile non è zero; quando arriva a zero è **estinto**. Lo stato è derivato, non memorizzato, e la lista può nascondere gli estinti.
7. Un conto di tipo Prestiti con prestiti registrati non può essere convertito a un altro tipo; gli altri conti possono cambiare tipo dal dettaglio.

2.2 Perimetro degli investimenti
Il sistema usa un unico perimetro globale di investimenti: non esistono portafogli multipli né un selettore di portafoglio. Tutti i valori sono in EUR.

La posizione è identificata dall'Instrument. La quantità detenuta e il costo residuo sono ricostruiti dagli eventi d'investimento validi.

2.3 Instrument
Un Instrument è una voce unica del catalogo. Conserva identificativo, nome, classe di attività, unità di prezzo e stato (Active oppure Archived). La classe di attività distingue azionario, obbligazionario, monetario, immobiliare, materie prime, cripto, bilanciato, previdenza e altro. Può inoltre conservare una sigla per il servizio di pricing, un codice ISIN, un link alla scheda descrittiva, una nota breve e il valore nominale per unità in EUR.

Per le obbligazioni può conservare la data di scadenza e il rendimento annuo fisso. La frequenza delle cedole e l'emittente non sono implementati. Uno strumento con storia non viene eliminato: viene archiviato dopo la vendita totale delle quote; l'eliminazione fisica è ammessa solo se non esistono operazioni né quotazioni.

I prezzi di mercato e il costo medio ponderato sono espressi in EUR. Non esistono valute di quotazione diverse dall'EUR.

2.4 Category
Una Category classifica esclusivamente entrate e spese ordinarie. Ha nome (categoria), sottocategoria, tipo (Income oppure Expense), descrizione opzionale e stato. La sottocategoria è obbligatoria nella creazione e modifica dalla UI; i dati preesistenti senza sottocategoria restano leggibili. Non classifica trasferimenti o operazioni d'investimento.

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
Income ed Expense contengono conto, categoria e importo positivo in EUR.
- Income produce un posting positivo sul conto.
- Expense produce un posting negativo sul conto.
Non si memorizzano importi negativi nelle proprietà di importo dell'operazione: il segno compare una sola volta nel posting.

3.3 Transfer
Un Transfer sposta liquidità tra un conto sorgente e un conto destinazione. Registra gli importi positivi dei due lati, entrambi in EUR e coincidenti.

Un trasferimento produce due posting di capitale: addebito sul conto sorgente e accredito sul conto destinazione. Eventuali commissioni sono componenti esplicite, con un posting negativo sul conto che le sostiene. Il trasferimento non è un'entrata o una spesa e non deve alterare di per sé il patrimonio consolidato.

3.4 InvestmentOperation
Un'operazione d'investimento è collegata a uno strumento. Quando comporta liquidità, è collegata anche a un conto di regolamento. Tutti gli importi sono in EUR.

Tipi inclusi nella prima versione:
- Buy: acquisto di quantità positiva a prezzo unitario positivo; genera un addebito di capitale e gli eventuali addebiti delle commissioni.
- Sell: vendita di quantità positiva non superiore alla quantità detenuta alla data economica; genera un accredito di capitale e gli eventuali addebiti delle commissioni.
- Dividend: provento da dividendo accreditato al conto; non modifica la quantità.
- Coupon: provento da cedola accreditato al conto; non modifica la quantità.
- StandaloneFee: commissione autonoma collegata, quando applicabile, allo strumento; addebita un conto ma non modifica quantità e costo base.
TransferIn, TransferOut e QuantityEvent sono concetti futuri. Non devono essere simulati come acquisti o vendite: un trasferimento di titoli deve preservare il costo storico e non generare un risultato realizzato.

Gli importi lordi, le commissioni, le eventuali ritenute fiscali e gli importi effettivamente regolati vanno distinti. Una ritenuta (TaxWithheld) non è una commissione: il trattamento fiscale completo è fuori dalla prima versione, ma i dati lordi e la ritenuta non devono essere confusi o persi.

3.5 AccountPosting
Ogni componente monetaria che modifica un conto produce un AccountPosting. Il posting conserva almeno:
- identificativo dell'operazione;
- conto;
- tipo di posting (Capital, Fee, Income, TransferLeg o equivalente);
- valuta EUR;
- SignedAmount, in EUR;
- riferimento alla componente originaria.
Un posting non duplica la fonte di verità: è la rappresentazione contabile del movimento che incide sul saldo. Ogni componente monetaria deve incidere sul conto una sola volta.

4. Segni e storni
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

4.2 Storni, correzioni e storia
La prima versione usa una strategia append-only con correzioni esplicite:
1. Un'operazione registrata non viene cancellata fisicamente né riscritta silenziosamente.
2. Uno storno è una ReversalOperation che identifica l'operazione originale e conserva il proprio momento di registrazione. Non crea un secondo movimento monetario o di quantità.
3. L'operazione originale resta consultabile ma viene esclusa dalle ricostruzioni economiche aggiornate quando è marcata come stornata.
4. La correzione è una nuova operazione, con i propri componenti e posting. Può avere OperationDate retrodatata, mentre RecordedAt indica quando la correzione è stata inserita.
5. La vista economica aggiornata a T include le operazioni attualmente valide con OperationDate ≤ T, indipendentemente da RecordedAt. Di conseguenza una correzione inserita oggi può cambiare i risultati ricostruiti di date passate: è il comportamento intenzionale della prima versione.
6. La domanda «che cosa risultava noto all'applicazione in una data passata?» richiede una vista bitemporale, futura. RecordedAt e la traccia degli storni vengono conservati per consentire tale evoluzione.
Una ReversalOperation non deve essere interpretata come un posting negativo che si somma al posting originale mentre l'originale rimane incluso. In questa strategia l'originale viene escluso dalla vista economica corrente e la correzione, se presente, entra una sola volta.

5. Prezzi, obbligazioni e valutazioni
5.1 PriceObservation
Una PriceObservation conserva strumento, prezzo positivo in EUR, unità di prezzo, ValidAt (quando il prezzo è valido), ObservedAt (quando è stato acquisito) e fonte. Le osservazioni sono append-only: un nuovo prezzo non sovrascrive quello precedente.

Per la valutazione alla data T si sceglie l'osservazione con ValidAt ≤ T più recente tra quelle ammissibili. Se l'età supera la soglia di obsolescenza configurata, la posizione è segnalata come non valutata o con prezzo obsoleto secondo una politica esplicita; non si presenta un prezzo vecchio come corrente senza indicazione. Non si usano mai prezzi futuri rispetto a T.

La pagina Investimenti consente di registrare una quotazione manuale con fonte «Manuale». Le osservazioni più vecchie di 30 giorni sono segnalate come da aggiornare, senza essere presentate come correnti.

ValuationPolicy descrive se lo strumento è valutato manualmente, tramite provider o con valore fisso, oltre alla soglia di obsolescenza. Un valore fisso è una politica esplicita, non una scorciatoia che elimina la provenienza del valore.

5.2 PriceUnit e obbligazioni
- PerUnit: prezzo espresso in EUR per una unità dello strumento. MarketValue = Quantity × Price.
- PercentOfNominal: prezzo espresso come percentuale del valore nominale per unità. EffectiveUnitPrice = NotionalValue × Price / 100; MarketValue = Quantity × EffectiveUnitPrice.
Esempio: nominale di 1.000 EUR, prezzo al 98,5%: prezzo unitario effettivo 985 EUR; per 3 unità, valore di mercato 2.955 EUR.

Il valore nominale, la scadenza e il rendimento annuo fisso sono caratteristiche dello strumento, non prezzi di mercato. I ratei maturati non sono inclusi nel valore di mercato definito qui: quando saranno implementati, il valore comprensivo di ratei sarà una metrica distinta. Le cedole sono proventi registrati come Coupon; non si incorporano nel prezzo. Le commissioni restano componenti separate.

6. Costo base e risultati d'investimento
6.1 Convenzioni contabili
Le metriche di investimento sono distinte dal patrimonio netto:
- NetWorth(T): saldi della liquidità più valore di mercato delle posizioni alla data T, tutti in EUR. È una fotografia patrimoniale. I saldi dei conti provengono dalle rilevazioni manuali quando queste sono fornite al calcolo: per ogni conto si usa l'ultima rilevazione con ValidAt ≤ T e un conto senza rilevazione valida rende il conto incompleto, senza sostituirne il saldo con zero. La scelta della modalità è esplicita e responsabilità del chiamante: fornire le rilevazioni seleziona il saldo manuale, ometterle seleziona il consuntivo dei posting; non esiste una inferenza automatica tra le due modalità e i due valori non si sommano mai.
- Risultato economico cumulato: guadagni/perdite realizzati, guadagni/perdite non realizzati, proventi e commissioni autonome, senza sommare nuovamente il saldo di cassa.
- Risultato di periodo: variazione del risultato cumulato tra due date, con trattamento coerente delle variazioni di valutazione e dei flussi.
Non si deve sommare NetWorth al risultato economico: sono misure diverse. In particolare, un dividendo aumenta la liquidità e contribuisce al risultato economico come provento, ma non va aggiunto due volte nella stessa metrica.

6.2 Costo medio ponderato
Il costo medio ponderato è una convenzione di reportistica personale, non un metodo fiscale.

Per ogni acquisto valido:
AcquisitionCost = Quantity × UnitPrice + BuyFees
AverageCost = TotalRemainingCost / QuantityHeld

Le commissioni d'acquisto sono già in EUR e sono incluse nel costo base.

Una vendita riduce quantità e costo base in proporzione al costo medio immediatamente precedente alla vendita. Il costo unitario medio non cambia a seguito della vendita stessa; cambia con nuovi acquisti o altri eventi di quantità esplicitamente definiti.

6.3 Formule
Per una posizione di un Instrument alla data T:
QuantityHeld(T) = somma delle quantità firmate degli eventi validi fino a T
CostBasis(T) = costo base residuo, incluse le commissioni di acquisto
AverageCost(T) = CostBasis(T) / QuantityHeld(T), se QuantityHeld(T) > 0
PositionValue(T) = QuantityHeld(T) × prezzo unitario applicabile a T
UnrealizedPL(T) = PositionValue(T) − CostBasis(T)
Per ogni vendita:
NetSaleProceeds = GrossSaleProceeds − SellFees
RealizedPLForSale = NetSaleProceeds − CostBasisAllocatedToSoldQuantity
CostBasisAllocatedToSoldQuantity usa il costo medio ponderato immediatamente prima della vendita. Se il prezzo è espresso come percentuale del nominale, il prezzo unitario effettivo viene calcolato prima di applicare la formula.

I proventi cumulati (Income) sono la somma dei dividendi e delle cedole validi, in EUR. Le commissioni autonome (OtherFees) sono sommate separatamente. Le commissioni d'acquisto sono già incluse nel costo base e quelle di vendita già incluse nei proventi netti: nessuna delle due viene sottratta una seconda volta.
TotalResult(T) = RealizedPL(T)
              + UnrealizedPL(T)
              + Income(T)
              − OtherFees(T)
Questa formula rappresenta il risultato economico cumulato del perimetro d'investimento, in EUR. Se la posizione non è valutabile per mancanza di prezzo, il risultato aggregato deve essere marcato incompleto, non calcolato fingendo che il valore mancante sia zero.

Per un intervallo [T1, T2]:
PeriodResult = TotalResult(T2) − TotalResult(T1)
La differenza dei risultati cumulati è la definizione di riferimento. Eventuali viste componenti di periodo devono essere coerenti con questa differenza, inclusa la variazione dell'utile/perdita non realizzato. Questa misura non è TWR o XIRR e non va presentata come rendimento percentuale.

7. Esempi numerici di verifica
Gli esempi usano importi decimal. Gli arrotondamenti sono mostrati solo per leggibilità.
7.1 Acquisto e rivalutazione
Acquisto di 2 unità a 100 EUR, senza commissioni: costo base 200 EUR. Alla data di valutazione il prezzo è 110 EUR: valore di mercato 220 EUR, UnrealizedPL = +20 EUR, RealizedPL = 0, proventi e commissioni autonome pari a zero. TotalResult = +20 EUR.
7.2 Acquisto con commissione e vendita parziale
Acquisto di 2,5 quote a 80 EUR con commissione di acquisto 1,50 EUR:
- costo base = 2,5 × 80 + 1,50 = 201,50 EUR;
- costo medio = 201,50 / 2,5 = 80,60 EUR.
Vendita di 1 quota a 95 EUR con commissione di vendita 1 EUR:
- provento netto = 95 − 1 = 94 EUR;
- costo allocato = 80,60 EUR;
- risultato realizzato = 94 − 80,60 = +13,40 EUR;
- quantità residua = 1,5; costo residuo = 120,90 EUR.
Con prezzo successivo pari a 90 EUR, il valore residuo è 135 EUR e l'utile non realizzato è 14,10 EUR. Risultato totale cumulato = 13,40 + 14,10 = +27,50 EUR. La verifica patrimoniale è −201,50 + 94 + 135 = +27,50 EUR.
7.3 Dividendo o cedola
Un provento di 12,50 EUR è accreditato sul conto. Il saldo aumenta di 12,50 EUR e il risultato economico include il provento una sola volta.
7.4 Commissione autonoma
Una StandaloneFee di 0,50 EUR riduce il conto di 0,50 EUR e viene sottratta dal risultato economico una volta. Non modifica quantità o costo base della posizione.
7.5 Storno e correzione
Un acquisto registrato per 2,5 quote viene stornato e sostituito con una nuova operazione corretta da 1,5 quote, con data economica originale e RecordedAt attuale. La ricostruzione economica aggiornata esclude l'acquisto stornato e include quello corretto. Non si crea un posting compensativo dello storno oltre alla sostituzione: il saldo aggiornato riflette solo l'operazione valida. La vista storica «come noto allora» resta futura.
7.6 Vendita eccedente
Se la posizione contiene 1 quota e viene tentata la vendita di 2 quote, l'operazione è rifiutata e non produce posting o variazioni di quantità. Le vendite allo scoperto non sono ammesse nella prima versione.

8. Invarianti di dominio
1. Gli importi, i prezzi, le commissioni e le quantità inseriti come magnitudini sono positivi; i valori zero sono ammessi solo dove una regola specifica lo consente.
2. Il segno monetario viene applicato una sola volta nei posting.
3. Ogni componente monetaria produce un effetto sul conto una sola volta; ogni posting appartiene a un'operazione e a un conto.
4. I posting sono espressi in EUR.
5. Buy e Sell non sono entrate/spese ordinarie. Dividendi e cedole non sono Income ordinari. I trasferimenti non sono entrate o spese.
6. Una vendita è valida solo se la quantità venduta non supera quella detenuta alla data economica, secondo l'insieme delle operazioni valide.
7. Le operazioni stornate sono escluse dalla ricostruzione economica aggiornata; storni e correzioni restano tracciabili.
8. Una valutazione storica usa solo osservazioni con ValidAt ≤ T. Prezzi mancanti o obsoleti generano uno stato di valutazione incompleta, non un valore inventato.
9. Prezzi, saldi, quantità, costi e risultati sono derivati dalle registrazioni; eventuali cache o proiezioni future devono poter essere ricostruite e riconciliate.
10. Il Domain non dipende da database, provider, protocolli HTTP o framework UI.
11. Non si sommano importi espressi in valute diverse: l'applicazione usa esclusivamente EUR.
12. Commissioni d'acquisto e vendita non vengono sottratte due volte; le commissioni autonome sono costi separati.
13. I valori derivati non valutabili propagano uno stato di incompletezza nei risultati aggregati.
14. Il saldo corrente di un Account è determinato dall'ultima rilevazione manuale registrata; le operazioni e i posting non lo modificano automaticamente.
15. Le rilevazioni manuali sono storiche e modificabili o eliminabili su azione esplicita dell'utente; ogni rilevazione è espressa in EUR.
16. L'assenza di una rilevazione manuale rende il saldo corrente esplicitamente non disponibile, mai uguale a zero per convenzione.
17. L'Application rifiuta una nuova rilevazione manuale con data economica successiva alla data corrente UTC; questa è una regola di ingresso applicativa e non introduce dipendenze temporali nel Domain.
18. Un trasferimento richiede due conti distinti e due importi positivi in EUR coincidenti.
19. Il costo residuo e tutti i risultati d'investimento sono espressi in EUR; non esistono conversioni da valute estere.

9. Prima versione e sviluppi futuri
9.1 Inclusi nella prima versione
- Account, AccountPosting, Category, Instrument;
- rilevazioni manuali dei saldi dei conti, con saldo corrente e consultazione storica;
- FinancialOperation con Income, Expense, Transfer, InvestmentOperation e ReversalOperation;
- componenti monetarie e di quantità, prezzi in EUR;
- operazioni di acquisto, vendita parziale, dividendo, cedola e commissione autonoma;
- saldi, quantità, costo base, costo medio, valore di posizione e patrimonio netto;
- risultato realizzato, non realizzato, proventi, commissioni autonome e risultato cumulato;
- osservazioni storiche di prezzi, con controlli su data e obsolescenza;
- storni append-only e correzioni retrodatate nella vista economica aggiornata.
9.2 Rinviati
- TWR, XIRR e classificazione definitiva dei flussi esterni;
- motore fiscale e regole fiscali specifiche;
- importazione CSV e deduplicazione tramite identificativi esterni;
- ricostruzione bitemporale «come noto alla data»;
- trasferimenti di titoli, operazioni societarie e altri eventi di quantità;
- ratei obbligazionari e valore comprensivo di ratei;
- provider reali e automazioni di aggiornamento.
I dati originari, le date, le osservazioni, gli importi lordi e le ritenute devono essere conservati quando disponibili, così da non precludere gli sviluppi futuri.

10. Decisioni di implementazione
1. Commissioni d'acquisto: incluse nel costo base e non sottratte di nuovo dal risultato.
2. Commissioni di vendita: sottratte dai proventi della vendita per calcolare il risultato realizzato.
3. Costo residuo: derivato dai costi storici degli eventi, tutti in EUR.
4. Trasferimenti: due conti distinti, importi EUR positivi e coincidenti; nessun cambio applicato.
5. Prezzi obsoleti: soglia predefinita configurabile in Application, con override per strumento; la posizione deve apparire non valutata o esplicitamente obsoleta oltre soglia.
6. Valuta: EUR unica per tutti i valori e per tutti i report.
7. TWR/XIRR: rimandati finché non è definita la distinzione tra flussi esterni, proventi e trasferimenti interni.
8. Trasferimento titoli: quando implementato, deve trasferire anche il costo storico residuo e non realizzare guadagni o perdite.
9. Ritenute fiscali: conservarle separate dalle commissioni, pur rinviando il calcolo fiscale.
10. Valutazioni incomplete: un report aggregato che dipende da prezzi mancanti deve dichiarare l'incompletezza invece di sostituire valori mancanti con zero.
