using Domina.Sim;

// Phase 1.6 — the batch simulation tool. It runs tens of thousands of fights without opening the
// engine and measures death/maiming/victory rates; all balance work rests on it.
// (bkz. docs/ROADMAP.md → Faz 1.6).
return SimCli.Run(args, Console.Out, Console.Error);
