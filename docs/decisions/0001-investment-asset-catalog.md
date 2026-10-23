# 0001 — Catalogo strumenti e gestione delle operazioni d'investimento

## Contesto

La pagina Investimenti mostrava solo le posizioni aperte ricostruite dalle operazioni. Non
esisteva un modo per creare o configurare uno strumento, registrare acquisti e vendite, inserire
una quotazione o archiviare uno strumento chiuso. Il modello distingueva gli strumenti con un
`InstrumentType` tecnico (azione, ETF, fondo, obbligazione, altro), insufficiente per classificare
un ETF azionario rispetto a un ETF monetario o obbligazionario.

## Decisione

- `InstrumentType` è sostituito da `AssetClass`: azionario, obbligazionario, monetario,
  immobiliare, materie prime, cripto, bilanciato, previdenza e altro.
- `Instrument` conserva inoltre sigla per il pricing, ISIN, link alla scheda, nota breve, stato
  (`Active`/`Archived`), scadenza e rendimento annuo fisso per le obbligazioni.
- Le operazioni d'investimento (acquisto, vendita, dividendo, cedola) si registrano, modificano ed
  eliminano dalla pagina; la validità dell'intera catena del portafoglio viene ricontrollata dopo
  ogni modifica.
- L'archiviazione è la disabilitazione: richiede quantità pari a zero e si ottiene vendendo tutte le
  quote. Uno strumento con storia non si elimina.
- Le quotazioni si inseriscono manualmente dalla pagina con fonte `Manuale`; restano osservazioni
  append-only e il servizio di pricing esterno potrà alimentare la stessa serie tramite la sigla.
- Se non esiste alcun portafoglio, all'avvio viene creato «Portafoglio principale».
  (Superato dall'ADR 0002: il concetto di portafoglio è stato eliminato.)

## Conseguenze

- La migrazione converte i vecchi tipi: azione e obbligazione mantengono la classe, fondo diventa
  bilanciato, ETF e altro diventano «Altro» e vanno riclassificati dalla pagina.
- Il trigger che impediva la cancellazione delle commissioni viene rimosso: è necessario per
  sostituire le commissioni quando un'operazione d'investimento viene modificata o eliminata. Le
  quotazioni restano protette dalla cancellazione.
- I totali restano non realizzati: valore attuale meno costo residuo. Dividendi e cedole sono
  esposti separatamente e non entrano nel rendimento percentuale.
- Un'osservazione più vecchia di 30 giorni è segnalata, non presentata come corrente.

## Alternative considerate

- **Mantenere `InstrumentType` accanto a `AssetClass`**: due classificazioni sovrapposte e più
  campi da compilare senza un vantaggio concreto.
- **Archiviare con un solo flag senza vendita**: lascerebbe quote aperte in uno strumento che
  l'utente considera disabilitato, alterando il patrimonio netto.
- **Gestire solo quotazioni importate**: la pagina non potrebbe mostrare il valore attuale finché
  il servizio esterno non è collegato.
- **Usare storni invece dell'eliminazione delle operazioni**: coerente con la storia contabile, ma
  più oneroso per correggere un inserimento errato; il repository ordinario usa già l'eliminazione
  fisica con conferma.
