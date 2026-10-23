# 0002 — Perimetro unico degli investimenti

## Contesto

Il modello iniziale prevedeva più `InvestmentPortfolio`, un selettore di portafoglio nella
registrazione delle operazioni e una dashboard con sezioni per portafoglio. Nell'uso reale
l'applicazione ha sempre un solo perimetro di investimenti: il portafoglio multiplo aggiungeva
selezioni, raggruppamenti e chiavi esterne senza un beneficio concreto.

## Decisione

- Il concetto di `InvestmentPortfolio` viene eliminato dal dominio, dall'applicazione e dalla
  persistenza. Le operazioni d'investimento sono globali e la posizione è identificata dal solo
  `Instrument`.
- La tabella `InvestmentPortfolios` e la colonna `FinancialOperations.PortfolioId` vengono
  rimosse con una migrazione; i check constraint delle operazioni non citano più il portafoglio.
- La pagina Investimenti, il patrimonio netto e la dashboard mostrano un unico insieme di
  posizioni; il dialogo operazioni non ha più il selettore di portafoglio.
- Il risultato mostrato è il risultato totale: plusvalenze realizzate + risultato non realizzato +
  dividendi e cedole − commissioni autonome. Il dettaglio per strumento espone le componenti.
- Gli ETF esistenti classificati come «Altro» vengono riclassificati in azionario, tranne XEON che
  passa a monetario.

## Conseguenze

- La migrazione `RemoveInvestmentPortfolios` elimina tabella e colonna e riaggancia le operazioni
  al perimetro unico. Il `Down` ricrea un portafoglio principale e riassegna le operazioni per
  poter ripristinare i check constraint.
- Il seeder del portafoglio predefinito e il relativo repository non esistono più.
- I calcolatori di posizioni, costo e valutazione non accettano più un portafoglio.
- I test di dominio, applicazione e integrazione sono stati aggiornati; le prove multi-portafoglio
  sono state rimosse perché il concetto non esiste più.

## Alternative considerate

- **Mantenere un portafoglio singleton nascosto**: avrebbe conservato la colonna e le chiavi
  esterne senza eliminare davvero il concetto.
- **Mantenere più portafogli**: utile solo con una gestione multi-conto/broker esplicita, non
  richiesta e non usata.
