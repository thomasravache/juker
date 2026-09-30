using Juker.TestKit;

// Executa o guard com WidgetsDbContext carregado neste processo; a exceção não tratada encerra com código != 0.
MigrationsGuard.AssertNoPendingModelChanges();
