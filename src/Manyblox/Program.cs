namespace Manyblox;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, _) => MessageBox.Show("A operação falhou. Nenhum detalhe privado foi registrado.", "Manyblox");
        try
        {
            using var store = new StateStore(StateStore.DefaultDirectory);
            var state = store.Load(); // Fail closed: never replace unreadable data with an empty database.
            Application.Run(new MainForm(store, state));
        }
        catch
        {
            MessageBox.Show("Não foi possível abrir os dados locais. Outra janela pode estar aberta, ou o arquivo pode estar danificado ou pertencer a outro usuário. Os dados existentes foram preservados.", "Manyblox", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
