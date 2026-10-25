# Design system MiddiFranchi

Identità visiva dell'applicazione: **product UI finanziaria**, colorata ma sobria, fondata su
token CSS centralizzati. Nessuna dipendenza UI esterna: il sistema è `app.css` + componenti
Blazor condivisi.

## Principi

- **Registrazione**: strumento quotidiano. Ogni decisione visiva serve la lettura dei dati.
- **Colore non decorativo**: blu e teal costruiscono la fiducia; l'ambra è l'accento raro
  (<10%: azione primaria "Nuova operazione", evidenze puntuali). I colori semantici
  (entrata/uscita/positivo/negativo) sono gli stessi in tutta l'app e **non bastano da soli**
  (testo o icona affiancata sempre presente).
- **Nessuna stima**: i dati incompleti si mostrano come incompleti (chip/motivi), mai a zero.

## Token (`wwwroot/app.css`, prefisso `--mf-`)

| Gruppo | Token | Note |
|---|---|---|
| Brand | `--mf-blue-900…100`, `--mf-teal-600/700/100`, `--mf-amber-500/700/100` | blu per rilevanza strutturale, teal per supporto grafico, ambra accento |
| Semantici | `--mf-income(-bg/-border)`, `--mf-expense(-bg/-border)`, `--mf-warning…`, `--mf-info…` | coerenti con entrate, uscite, attenzioni |
| Superfici | `--mf-bg`, `--mf-surface`, `--mf-surface-muted`, `--mf-surface-inset`, `--mf-sidebar*` | sfondo tinto di blu, superfici discriminate su 3 livelli |
| Testo | `--mf-text`, `--mf-text-secondary`, `--mf-text-muted` | gerarchia a 3 livelli |
| Bordi | `--mf-border`, `--mf-border-strong` | |
| Raggi | `--mf-radius-s/m/l` | 8/12/18 |
| Ombre | `--mf-shadow-sm/md/lg` | elevate con ombre tinte di blu |
| Spazi | `--mf-space-1/2/3/4/6/9` | 4/8/12/16/24/36 (scala 1–4–9) |
| Tipografia | `--mf-font-display`, `--mf-font-body`, `--mf-text-xs…2xl` | Bricolage Grotesque (display), Onest (corpo); `tabular-nums` su importi e tabelle |

Il tema scuro riusa gli stessi token: chiave `data-theme="dark"` su `<html>`
(automatico con `prefers-color-scheme`, script in `App.razor`).

## Componenti condivisi (`Components/Shared/`)

| Componente | Uso |
|---|---|
| `MfIcon` | icone SVG inline (stroke, `currentColor`); uso `Name` della raccolta interna |
| `AmountValue` | importo con segno semantico (positivo/zero/negativo), testuale `Non disponibile` quando assente |
| `StatusBadge` | chip di stato (`neutral`, `brand`, `teal`, `amber`, `income`, `expense`, `warning`, `info`) |
| `StatTile` | dato di punta; variante `Hero` (blu scuro) per il patrimonio |
| `EmptyState` | vuoti con significato: titolo, spiegazione, azione di uscita |
| `MiniBarChart` | barre di distribuzione da valori reali (nessun dato fittizio) |
| `ManualBalanceDialog` | modale per registrare una rilevazione manuale; il conto è scelto da un menu a tendina con avatar e icone |
| `NewLoanDialog` | modale per aggiungere un prestito al conto Prestiti (debitore, importo iniziale, data) |
| `LoanReadingDialog` | modale per aggiungere una rilevazione a un singolo prestito |
| `AccountSettingsDialog` | impostazioni del conto: nome, tipo, stato, colore dei grafici e icona caricata |
| `CategoryManagementPanel` | elenco e creazione categorie con icona e colore, riusato da `/categories` e dalla Configurazione |
| `CategoryBreakdownDialog` | modale con il donut delle sottocategorie di una categoria di spesa, aperto dal pie chart della pagina Movimenti |

Classi strutturali: `.page` (shell), `.app-sidebar` (rail di icone, vedi "Sidebar" sotto), `.topbar`, `.page-body`,
`.bento` (griglia 12 colonne con `.sp-3…sp-12`), `.card`, `.data-table` (con `.num` per importi
e scroll orizzontale con prima colonna sticky sui mobili), `.table-scroll` (contenitore tabella
scrollabile e focusabile da tastiera), `.form-grid`/`.form-inline`, `.accent-button`,
`.primary-button`, `.secondary-button`, `.danger-button` (azioni distruttive confermate),
`.status-chip`, `.alert(-error/-success/-warning/-info)`, `.inline-edit` (campi compatti nelle
tabelle), `.empty-state`, `.distribution`, `.pagination`. Sui mobili la caption delle tabelle resta
per gli screen reader ma non viene mostrata; nella tabella delle operazioni l'azione resta sticky a
destra.

## Dashboard (`/`)

La Home usa classi dedicate, tutte costruite sui token esistenti, per non alterare le altre pagine.

| Pattern | Uso |
|---|---|
| `.dashboard-overview` | Vedi "Panoramica dashboard": il totale patrimonio vive qui, non in una card separata. |
| `.section-head` | Titolo di sezione con icona e link contestuale. |
| `.dashboard-movements-card` | Card unica "Movimenti": header con periodo e link a tutti i movimenti; `.movements-summary` con saldo, entrate e uscite ordinarie del mese in EUR (trasferimenti e investimenti esclusi e conteggiati a parte); a seguire `.movements-recent` con badge del conteggio mensile e `.activity-list` compatta (una riga per operazione, massimo 6). Le righe mostrano categoria · sottocategoria, icona/colore della categoria, data e importo: il segno/colore dell'importo distingue entrata e uscita e non viene ripetuto il tipo. La sintesi del mese è orizzontale su desktop e si impila sotto 980px. |
| `.dashboard-allocation` | Card «Ripartizione del patrimonio», sopra i Movimenti: donut SVG investimenti/conti con totale al centro, percentuali e importi nella legenda; barra superiore bicolore. Le righe sono link a `/investments` e `/accounts`, con freccia e focus visibile. Chip "Parziale" se una categoria non è completa e stato esplicito se la ripartizione non è calcolabile (con link alle due pagine). |

Responsive: la dashboard usa `.bento` a 12 colonne con card a piena larghezza; le card si impilano
sotto 980px. Sotto 640px le righe delle attività passano a due colonne con l'importo sotto il testo;
sotto 520px le righe della ripartizione portano importo e freccia sotto la barra. Gli importi usano
`tabular-nums` e non vengono mai sovrapposti o tagliati.

Accessibilità della dashboard: i motivi dell'incompletezza sono un elenco testuale, le posizioni
non valorizzate usano `<details>/<summary>` con focus visibile, ogni stato ha un'etichetta scritta
(completo, incompleto, non registrato, non calcolabile) e nessuna informazione dipende dal solo
colore.

## Panoramica dashboard (`/`)

- `.dashboard-overview`: card in testa alla Home e unico punto in cui si legge il patrimonio.
  Selettori **Investimenti/Conti** multiselezionabili (entrambi di default), valori correnti e
  selettore d'intervallo (1G, 7G, 1M, Anno, 1A, 3A, Max). "Anno" mostra l'anno corrente dal
  1° gennaio. Con entrambe le categorie attive, o con il solo
  Totale, il valore grande è la somma.
- La testata mostra il **totale combinato come valore grande**: con entrambe le categorie attive
  o con il solo Totale è la somma degli investimenti e dei conti; con una sola categoria attiva è il
  valore di quella. Se una categoria è incompleta, il valore è la somma dei dati disponibili e
  accanto compare il chip **Parziale** (i conti senza saldo manuale restano esclusi, mai zero).
  Sotto il totale: per gli investimenti rendimento assoluto e percentuale con capitale versato
  ("+€X +Y% su €Z versati"); per i soli conti la variazione nel periodo selezionato.
  Con entrambe le categorie attive, o con il solo Totale, una riga di dettaglio etichetta il
  valore di ciascuna.
- Il grafico è ApexCharts e mostra il valore degli investimenti e il totale dei conti quando
  entrambe le categorie sono attive; con i soli investimenti mostra anche il capitale investito;
  con il solo Totale mostra la linea del totale.
  Con entrambe attive le due serie sono linee, senza aree. I conti usano l'azzurro, gli
  investimenti l'arancio.
- I selettori e gli intervalli sono `button` reali con `aria-pressed`; il grafico si ricrea al cambio
  di categoria o intervallo. Nella Panoramica non ci sono marker né tabella dati: il tooltip mostra
  il valore passando sulla linea.
- Per i conti il totale usa il saldo valido alla data, come il patrimonio netto; la disponibilità
  non è più mostrata separatamente. I conti senza rilevazione non vengono trattati come zero.
- Per gli investimenti la variazione mostrata è quella corrente rispetto al capitale investito; i
  punti storici senza quotazioni non vengono interpolati.
- La card «Ripartizione del patrimonio», subito sopra i Movimenti, usa un donut SVG senza
  dipendenze aggiuntive: quota di investimenti e conti sul totale disponibile, totale al centro,
  percentuali e importi nella legenda. Ogni riga è un link: Investimenti apre `/investments`,
  Conti apre `/accounts`. Se un valore manca, il totale è nullo o negativo, la card mostra uno
  stato esplicito («Ripartizione non calcolabile») con i link alle due pagine e non stima la parte
  mancante.
- Responsive: tab e intervalli vanno a capo, il valore usa una dimensione fluida e il grafico resta
  dentro la card senza overflow.

## Movimenti (`/operations`)

- La pagina si chiama **Movimenti** (la voce di sidebar precedente «Operazioni» è stata
  rinominata); niente introduzione, header con icona e periodo corrente.
- Il pulsante flottante `+` apre il modale `OperationDialog`: solo **Entrata** o **Uscita**
  (nessun trasferimento), senza conto. Per le entrate c'è un unico menu categoria (nessuna
  sottocategoria); per le uscite ci sono due menu: **Categoria** e **Sottocategoria**. Se si
  sceglie solo la sottocategoria dalla lista completa, la categoria si popola automaticamente.
  Un solo campo **Descrizione / Nota** (il vecchio campo Note è stato unificato e rimosso dal
  database con `MergeOperationDescriptionAndNotes`). Entrate e uscite non generano posting: i
  saldi dei conti restano quelli manuali.
- `.operations-history-card` è a piena larghezza ed è la card **Lista**: i tre pulsanti
  **Tutti / Entrate / Uscite** sono allineati a destra sulla stessa riga del titolo. La tabella
  ha colonne fisse **Data, Categoria, Sottocategoria, Descrizione, Importo** (le celle vuote
  restano vuote, senza «-») e una riga di filtri per ogni colonna: data, categoria,
  sottocategoria, testo della descrizione e importo.
- Cliccando una riga si apre `OperationEditDialog`: categoria/sottocategoria, importo, data e
  descrizione modificabili. L'eliminazione è nella zona pericolosa in fondo al modale (stesso
  stile del pannello conti, con icona cestino e conferma) e usa `OperationDeletionService`. La
  vecchia pagina `/movements` è stata rimossa: il link della Dashboard punta qui.
- In fondo, `.operations-trend-card` mostra un istogramma impilato mensile di Entrate e Uscite con
  la linea del Saldo e selettore 6M/12M/Max. `.operations-pie-card` raggruppa le uscite **solo per
  categoria** (prime 6 + «Altre categorie»); cliccando una categoria si apre
  `CategoryBreakdownDialog`, che mostra lo stesso donut limitato alle sue **sottocategorie**.
- La migration `AddHistoricalDemoMovements` inserisce movimenti dimostrativi dal 2023 al 2026 per
  rendere leggibili i grafici storici.
- `DefaultCategoriesSeeder` inserisce all'avvio, in modo idempotente, le categorie di spesa di
  esempio (con icona e colore) e le categorie di entrata senza sottocategorie (Stipendio, Regali,
  ExtraStipendio, Patto, Concorrenza, Premio Produzione, Rimborsi, AltroIncome), ciascuna con la
  propria icona e colore. Lo stesso elenco è usato dalle migration di reset per il database
  esistente.

## Investimenti (`/investments`)

- `.investment-summary-card`: quattro KPI senza sottotesto (valore attuale, prezzo di carico,
  rendimento, cedole e dividendi). Il rendimento usa `.positive`/`.negative` per il colore.
- Il grafico usa l'asse X temporale e mostra due serie: valore attuale e capitale investito, con una
  legenda compatta. Intestazione, piè di pagina e tabella dati del componente sono disattivati.
- `.investment-allocation-card`: torta in stile Conti, senza legenda. Il passaggio del mouse su una
  fetta mostra classe, valore e quota al centro; il centro predefinito mostra il totale.
- `.investments-table`: intestazioni ordinabili, ricerca compatta, pillole multi-selezione per
  classe e attivazione degli archiviati. Lo stato delle quotazioni è in alto a destra della card:
  verde se tutte le posizioni hanno una quotazione odierna, giallo altrimenti, con l'elenco degli
  strumenti non aggiornati nel tooltip.
- Le righe mostrano il tag della classe accanto al ticker, il prezzo attuale prima del prezzo
  medio (con la percentuale del nominale per le obbligazioni) e nessuna colonna con la data di
  quotazione. Ogni riga si espande su metriche, dati dell'obbligazione, collegamenti, note e
  operazioni.
- Il rendimento è il risultato totale: realizzato + non realizzato + proventi − commissioni
  autonome. Il dettaglio della riga espone le singole componenti.
- Non esiste il concetto di portafoglio: il perimetro degli investimenti è unico.
- Responsive: le colonne secondarie si nascondono, il dettaglio passa a una colonna e il grafico
  resta dentro la card senza allargare la pagina.

## Grafici temporali

- `.library-chart`: figura con titolo, descrizione, grafico **Blazor-ApexCharts 7.0.0**
  (MIT), nota e tabella dati richiudibile. È l'unico grafico temporale delle pagine Investimenti e
  Conti. Il componente `ApexTimeSeriesChart` disegna linee per impostazione predefinita; le serie
  con `Kind = Bar` diventano barre e, con `Stacked`, vengono impilate mentre le serie di linea
  restano sovrapposte (grafico misto). Il tooltip mostra il dettaglio per serie e, se
  `ShowStackedTotal` è attivo, anche il totale.
- Il tema Apex segue il toggle chiaro/scuro dell'app senza ricaricare la pagina; colori, assi,
  tooltip e griglia sono coerenti con i token dell'interfaccia.
- L'asse X è temporale e i punti sono le date realmente disponibili (quotazioni, operazioni o
  rilevazioni manuali). I valori mancanti non vengono interpolati né sostituiti con zero.
- Etichette dell'asse X nella Panoramica: 1G ogni 6 ore, 7G un giorno per tick, 1A/3A mese-anno,
  Max anno a gennaio (mese-anno tra un anno e l'altro). I limiti dell'asse sono ancorati alla
  finestra scelta; per "Anno" e per Max l'asse parte dal 1° gennaio (dell'anno corrente o del
  primo anno con dati).
- L'asse Y è in EUR; supporta valori negativi (per esempio carte di credito).
- `.library-chart-canvas` imposta larghezza 100% e altezza 320px; su mobile il grafico resta dentro
  la card senza allargare la pagina.
- `.library-chart-data` espone una tabella accessibile con una riga per data e una colonna per serie,
  così i valori restano disponibili anche senza interazione col grafico.
- Conti (`/accounts`): la card si chiama **«Valore conti»** e mostra in testa il **totale attuale**
  di tutti i conti (con chip «Parziale» se qualcuno non ha saldo). Il grafico è misto: una barra
  impilata per mese e una linea del totale. Ogni barra è composta dal saldo manuale valido a fine
  mese dei singoli conti; per ogni mese si usa l'ultima rilevazione disponibile e i conti senza
  rilevazione non sono conteggiati come zero. La linea rappresenta sempre la somma di tutti i conti.
  Il selettore (**Anno, 1A, 3A, Max**) filtra i mesi mostrati.
  Al posto della tabella dati il grafico mostra una griglia `.chart-stats` con valore massimo,
  minimo, medio, variazione media mensile e variazione media annuale, calcolati sull'intervallo
  selezionato.
  Il pulsante flottante apre il modale per registrare una nuova rilevazione: il conto si sceglie da
  un menu a tendina con avatar/icona a sinistra del nome e il conto Prestiti è escluso (si gestisce
  dai suoi prestiti). L'elenco conti non mostra più il saldo ricostruito né il modulo «Nuovo conto».
  Il conteggio dei conti è affiancato al titolo e il tasto «Nascondi chiusi» filtra i conti con stato
  Chiuso; la riga mostra solo la data della rilevazione, senza tipo né «rilevato al».

## Dettaglio conto (`/accounts/{id}`)

- La card «Saldo nel tempo» mostra solo il saldo corrente con la data della rilevazione e il
  grafico; il saldo ricostruito non è più duplicato nella card. Il selettore di intervallo filtra le
  date. Con meno di due date compare un empty state.
- Lo storico ha tre colonne (Data, Registrata il, Importo) senza colonna Azioni: cliccando una
  qualsiasi cella della riga si apre il pannellino con importo e data modificabili, «Salva»,
  «Elimina» (con conferma) e «Chiudi». Dopo ogni modifica saldo e grafico si ricaricano.
- Il pulsante flottante apre il modale «Nuova rilevazione» con il conto fisso (avatar e nome
  accanto). La vecchia card di registrazione non esiste più.
- L'ingranaggio in alto a destra apre `AccountSettingsDialog`: nome, stato (Attivo/Chiuso),
  colore personalizzato dei grafici a barre e icona (PNG/JPEG/WebP fino a 512 KB, salvata come data
  URL). L'icona personalizzata ha priorità sul logo e sull'icona predefinita; il colore è usato
  nelle barre della pagina Conti. Il tipo non è modificabile dalla UI.
- «Elimina conto» è nel modale di modifica del conto (icona ingranaggio) e richiede una conferma
  esplicita con `.danger-button`. L'eliminazione rimuove anche le rilevazioni manuali; un conto con
  operazioni registrate viene rifiutato dal servizio con un messaggio esplicito. I conti speciali
  mostrano «Conto speciale: non eliminabile».

## Configurazione (`/settings`)

- **Conti**: tabella Conto/Stato, senza colonne Tipo, Saldo manuale e Azioni; il nome del conto è un pulsante che
  apre `AccountSettingsDialog`. Il modale contiene anche «Elimina conto» con conferma. I conti
  speciali Cash e Prestiti sono marcati «Speciale» e non possono essere eliminati: possono essere
  disattivati dalle impostazioni. Un conto con operazioni registrate viene rifiutato dal servizio.
- **Aggiungi conto**: griglia di tessere predefinite con logo — BBVA, Banco di Sardegna, Directa,
  ING, Intesa San Paolo, PayPal, PostePay, Revolut, Unicredit — più la tessera «Nuovo conto» per un
  nome libero. Si seleziona una tessera e si conferma. I nuovi conti sono creati come Conto
  corrente; il tipo non è modificabile dalla UI. I loghi sono in `wwwroot/logos`. Dopo la creazione
  nome, stato, colore e icona si configurano dall'ingranaggio.
- **Categorie**: `CategoryManagementPanel` in layout bento (elenco + nuova categoria), con le
  colonne Categoria, Sottocategoria, Tipo e Descrizione; icone e colori (`.category-mark`) sono
  mostrati nell'elenco e modificabili da `CategorySettingsDialog`. Le categorie di uscita hanno
  una sottocategoria obbligatoria in creazione e modifica; le categorie di entrata possono non
  averla. Il nome apre `CategorySettingsDialog` per aggiornarla. Le categorie di spesa di esempio
  sono raggruppate in Casa & Utenze,
  Trasporti, Auto, Fees, Fun & Shopping, Salute & Benessere, Alimentazione, Streaming e Varie,
  ciascuna con un'icona e un colore univoco. La migration
  `AddCategoryAppearanceAndResetExpenseCategories` ha sostituito le vecchie categorie di spesa
  riassegnando le operazioni esistenti. La stessa pagina `/categories` resta disponibile e usa lo
  stesso pannello.
- Il conteggio dei conti è affiancato al titolo «Conti».

## Conto Prestiti e singolo prestito

- I due conti speciali **Cash** (tipo Cassa, icona banconota) e **Prestiti** (tipo Prestiti, icona
  mano con moneta) vengono creati in modo idempotente all'avvio.
- Il conto Prestiti non usa le rilevazioni manuali dirette: ogni prestito ha le sue rilevazioni e
  il saldo del conto è la somma delle ultime rilevazioni di tutti i prestiti. La somma è
  materializzata come rilevazioni manuali aggregate, così dashboard, grafici e patrimonio netto
  vedono un unico saldo.
- Nel dettaglio del conto Prestiti la card «Prestiti» elenca i debitori con importo e data; il
  conteggio è affiancato al titolo. Un prestito arrivato a zero è marcato «Estinto» e il tasto
  «Nascondi estinti» lo rimuove dalla lista. Il pulsante flottante apre «Nuovo prestito».
- Ogni prestito ha una pagina dedicata `/accounts/{id}/loans/{loanId}` con grafico «Importo nel
  tempo», selettore Anno/1A/3A/Max, storico delle rilevazioni apribile cliccando la riga e
  eliminazione del prestito con conferma.
- I prestiti senza rilevazioni contribuiscono con zero alla somma del conto; il conto espone
  comunque un saldo (mai "Non calcolabile" per questo motivo).

## Sidebar

- Rail di icone da 68px, compressa per default. Il pulsante circolare a cavallo del bordo la
  espande a 252px con etichette e nome dell'app; lo stato è salvato in `mf-sidebar-collapsed`.
  Il marchio usa la stessa icona da 20px delle voci, centrata nella rail.
- Ordine senza titoli di gruppo: in alto Dashboard, Conti e Movimenti;
  poi Investimenti e Patrimonio netto; in fondo **Configurazione** (ingranaggio), separata da un
  divisore. La pagina Configurazione include anche la gestione delle categorie.
- Ogni voce ha un'accento proprio (`--mf-nav-*`, varianti scure dei token semantici: blu per la
  dashboard, azzurro conti, ambra operazioni, teal configurazione, arancio investimenti, verde
  patrimonio) applicato alla sola icona; le etichette restano neutre. Ogni voce mantiene il
  `title`/`aria-label` quando l'etichetta scritta è nascosta.
- Lo stato attivo è un riquadro con bordo e alone blu.
- Sotto 900px la sidebar diventa un pannello sovrapposto a larghezza piena, con etichette sempre
  visibili e chiusura tramite il pulsante X, il backdrop o Escape.

## Accessibilità

- `:focus-visible` con anello 2px coerente su ogni controllo; mai `outline: none` senza rimpiazzo.
- Messaggi dinamici: errori con `role="alert"`, conferme con `role="status"`.
- Form: `required` + `aria-required` su campi obbligatori, hint collegati con `aria-describedby`
  (es. `transfer-hint`, `reading-hint`), label sempre visibili, input ≥16px (no zoom iOS).
- Target ≥44px, aria-label su pulsanti icona e su azioni "Elimina" contextual.
- Tabelle con `thead`, `th scope="col"`, `caption` descrittiva.
- Grafici: `<figure>/<figcaption>`, grafico ApexCharts e tabella dati richiudibile con una riga per
  data; il contenitore della tabella è raggiungibile da tastiera. I controlli di categoria e
  intervallo sono pulsanti con `aria-pressed` e stato attivo visibile.
- `prefers-reduced-motion` disattiva le transizioni.
- `lang="it"` coerente con i contenuti.

## Pagine

| Route | Contenuto |
|---|---|
| `/` | Dashboard: panoramica interattiva Investimenti/Conti con intervalli e patrimonio, card Ripartizione del patrimonio (donut con accesso a Conti e Investimenti) e card Movimenti (entrate/uscite del mese in EUR e ultime operazioni) |
| `/accounts`, `/accounts/{id}` | elenco con istogramma mensile impilato e linea del totale, range temporale e modale di registrazione; dettaglio con saldo, data e grafico, storico apribile cliccando la riga ed eliminazione dal modale del conto. Per il conto Prestiti: elenco prestiti e gestione aggregata |
| `/accounts/{id}/loans/{loanId}` | dettaglio del singolo prestito con grafico dell'importo, rilevazioni modificabili/eliminabili ed eliminazione del prestito |
| `/operations` | pagina Movimenti: riepilogo del mese, card Lista a piena larghezza con filtri per colonna e modale di modifica/eliminazione per riga, dialog `+` per entrate/uscite, grafico mensile impilato e donut delle uscite per categoria con dettaglio per sottocategoria |
| `/categories` | categorie entrata/uscita (stesso pannello della Configurazione) |
| `/settings` | configurazione generale: conti (aggiunta, impostazioni, eliminazione) e categorie |
| `/investments` | cruscotto con KPI, grafico valore/capitale investito, torta per classe e tabella strumenti ordinabile con dettaglio operazioni |
| `/net-worth` | riepilogo patrimonio alla data selezionata |

Limitazioni dichiarate in pagina: le operazioni registrabili sono solo entrate e uscite (i
trasferimenti non sono esposti dalla UI); tutti i valori sono in EUR; nessuna evoluzione storica
del patrimonio (nessuno snapshot nel modello);
posizioni non valorizzate senza quotazione valida. La pagina Investimenti mostra "Sigla non
disponibile" per gli strumenti senza `Symbol`: la sigla non viene dedotta dal nome. I grafici
temporali usano Blazor-ApexCharts 7.0.0 (MIT), solo le date con dati registrati, e non interpolano;
il grafico dei conti è un istogramma mensile impilato con la linea del totale.
