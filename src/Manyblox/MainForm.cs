using System.Globalization;

namespace Manyblox;

public sealed class MainForm : Form
{
    private readonly StateStore store;
    private LocalState state;
    private readonly ListView accounts = new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = false, HideSelection = false };
    private readonly ListView instances = new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = false };
    private readonly TextBox search = new() { Width = 220, PlaceholderText = "Buscar nome, apelido ou categoria" };
    private readonly TextBox place = new() { Width = 150, PlaceholderText = "Place ID do jogo" };
    private readonly Label status = new() { Dock = DockStyle.Bottom, Height = 28, Padding = new Padding(8) };
    private bool refreshing;

    public MainForm(StateStore store, LocalState state)
    {
        this.store = store;
        this.state = state;
        Text = "Manyblox — Gerenciador local de contas Roblox";
        MinimumSize = new Size(880, 620);
        Size = new Size(1040, 720);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10);
        accounts.Columns.Add("Usuário", 200);
        accounts.Columns.Add("Apelido", 190);
        accounts.Columns.Add("Categoria", 160);
        accounts.Columns.Add("ID público", 170);
        instances.Columns.Add("PID", 90);
        instances.Columns.Add("Iniciada em", 180);
        instances.Columns.Add("Janela", 550);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        var accountTab = new TabPage("Contas");
        var instanceTab = new TabPage("Instâncias");
        tabs.TabPages.AddRange([accountTab, instanceTab]);
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 84, Padding = new Padding(6), WrapContents = true };
        toolbar.Controls.Add(search);
        AddButton(toolbar, "Adicionar", () => Edit(false));
        AddButton(toolbar, "Editar", () => Edit(true));
        AddButton(toolbar, "Remover", Remove);
        AddButton(toolbar, "Login oficial", Login);
        AddButton(toolbar, "Ver perfil", Profile);
        var launchbar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 54, Padding = new Padding(6) };
        launchbar.Controls.Add(place);
        AddButton(launchbar, "Abrir página do jogo", () => Launch(false));
        AddButton(launchbar, "Iniciar Roblox", () => Launch(true));
        var notice = new Label { Dock = DockStyle.Top, Height = 65, Padding = new Padding(8), Text =
            "Cadastro local: não autentica nem troca sessões. Faça login manual na conta desejada no Roblox.\nNão informe senhas ou cookies. O lançamento usa a sessão atual do cliente oficial." };
        accountTab.Controls.Add(accounts);
        accountTab.Controls.Add(launchbar);
        accountTab.Controls.Add(toolbar);
        accountTab.Controls.Add(notice);
        var instancebar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 50, Padding = new Padding(6) };
        AddButton(instancebar, "Atualizar instâncias", RefreshInstances);
        AddButton(instancebar, "Solicitar fechamento", CloseInstance);
        instanceTab.Controls.Add(instances);
        instanceTab.Controls.Add(instancebar);
        var instanceNotice = new Label { Dock = DockStyle.Bottom, Height = 65, Padding = new Padding(8), Text =
            "A lista identifica processos pelo nome e não comprova sua assinatura ou conta autenticada.\nO fechamento envia uma solicitação normal à janela selecionada. Não encerra processos à força.\nO cliente oficial decide se permite várias instâncias; não há alteração de mutex ou bypass." };
        instanceTab.Controls.Add(instanceNotice);
        Controls.Add(tabs);
        Controls.Add(status);
        search.TextChanged += (_, _) => RefreshAccounts();
        accounts.SelectedIndexChanged += (_, _) =>
        {
            if (!refreshing && Selected() is AccountEntry a)
                Run(() => Commit(state with { SelectedId = a.Id }));
        };
        accounts.DoubleClick += (_, _) => Run(() => Edit(true));
        place.Text = state.LastPlaceId > 0 ? state.LastPlaceId.ToString(CultureInfo.InvariantCulture) : "";
        RefreshAccounts();
        status.Text = "Dados protegidos por usuário do Windows. Sem telemetria ou atualização automática.";
    }

    private void AddButton(Control parent, string text, Action action)
    {
        var button = new Button { Text = text, AutoSize = true, Height = 32 };
        button.Click += (_, _) => Run(action);
        parent.Controls.Add(button);
    }

    private void Run(Action action)
    {
        try { action(); }
        catch (InvalidDataException) { MessageBox.Show(this, "Dados inválidos. Use um nome Roblox de 3 a 20 letras, números ou _. IDs devem ser números positivos. Não informe senhas ou cookies.", "Manyblox"); }
        catch { MessageBox.Show(this, "Não foi possível concluir a operação. Verifique a instalação do Roblox, as permissões locais ou atualize a lista de instâncias.", "Manyblox", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    private AccountEntry? Selected() => accounts.SelectedItems.Count == 1 ? accounts.SelectedItems[0].Tag as AccountEntry : null;
    private void Commit(LocalState next) { store.Save(next); state = next; }

    private void RefreshAccounts()
    {
        refreshing = true;
        accounts.BeginUpdate();
        try
        {
            accounts.Items.Clear();
            foreach (var a in state.Accounts.OrderBy(a => a.Category).ThenBy(a => a.Username))
            {
                if (!(a.Username + " " + a.Alias + " " + a.Category).Contains(search.Text, StringComparison.OrdinalIgnoreCase)) continue;
                var item = new ListViewItem([a.Username, a.Alias, a.Category, a.UserId == 0 ? "Não informado" : a.UserId.ToString(CultureInfo.InvariantCulture)]) { Tag = a };
                accounts.Items.Add(item);
                if (a.Id == state.SelectedId) item.Selected = true;
            }
        }
        finally { accounts.EndUpdate(); refreshing = false; }
    }

    private void Edit(bool existing)
    {
        var current = existing ? Selected() : null;
        if (existing && current is null) { status.Text = "Selecione uma conta para editar."; return; }
        using var editor = new AccountEditor(current);
        if (editor.ShowDialog(this) != DialogResult.OK || editor.Entry is null) return;
        var list = state.Accounts.Where(a => a.Id != editor.Entry.Id).ToList();
        list.Add(editor.Entry);
        Commit(state with { Accounts = list, SelectedId = editor.Entry.Id });
        RefreshAccounts();
        status.Text = "Cadastro salvo. A conta ainda precisa de login manual no Roblox.";
    }

    private void Remove()
    {
        if (Selected() is not AccountEntry a) return;
        if (MessageBox.Show(this, $"Remover o cadastro local de {a.Username}? Isso não remove a conta Roblox.", "Manyblox", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        Commit(state with { Accounts = state.Accounts.Where(x => x.Id != a.Id).ToList(), SelectedId = null });
        RefreshAccounts();
    }

    private void Login()
    {
        MessageBox.Show(this, "O navegador padrão abrirá o login oficial. Confira a conta autenticada e troque-a manualmente se necessário. O Manyblox não lê essa sessão.", "Login oficial");
        RobloxActions.Open(RobloxActions.LoginUri);
    }

    private void Profile()
    {
        if (Selected() is not AccountEntry a || a.UserId == 0) { status.Text = "Selecione uma conta com ID público informado."; return; }
        RobloxActions.Open(RobloxActions.ProfileUri(a.UserId));
    }

    private void Launch(bool direct)
    {
        long id = Rules.ParseId(place.Text);
        if (MessageBox.Show(this, "O Roblox usará a sessão atual, que pode ser diferente do cadastro selecionado. Confirme que fez login na conta desejada. Continuar?", "Abrir jogo", MessageBoxButtons.YesNo, MessageBoxIcon.Information) != DialogResult.Yes) return;
        RobloxActions.Open(direct ? RobloxActions.LaunchUri(id) : RobloxActions.GameUri(id));
        Commit(state with { LastPlaceId = id });
        status.Text = "Abertura solicitada. A autenticação e o início do jogo são controlados pelo Roblox.";
    }

    private void RefreshInstances()
    {
        instances.Items.Clear();
        foreach (var entry in InstanceMonitor.List())
            instances.Items.Add(new ListViewItem([entry.Pid.ToString(CultureInfo.InvariantCulture), entry.Started.ToString("g"), entry.Title]) { Tag = entry });
        status.Text = $"{instances.Items.Count} janela(s) encontrada(s).";
    }

    private void CloseInstance()
    {
        if (instances.SelectedItems.Count != 1 || instances.SelectedItems[0].Tag is not InstanceEntry entry) return;
        if (MessageBox.Show(this, $"Solicitar fechamento da janela PID {entry.Pid}? O jogo pode ser interrompido.", "Fechar janela", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        status.Text = InstanceMonitor.RequestClose(entry) ? "Fechamento solicitado; a janela pode recusar." : "A janela não aceitou a solicitação.";
        RefreshInstances();
    }
}

internal sealed class AccountEditor : Form
{
    public AccountEntry? Entry { get; private set; }
    public AccountEditor(AccountEntry? original)
    {
        Text = original is null ? "Adicionar cadastro" : "Editar cadastro";
        ClientSize = new Size(500, 310);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(16) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        TextBox Field(string label, string value, int limit)
        {
            int row = layout.RowCount++;
            layout.Controls.Add(new Label { Text = label, AutoSize = true }, 0, row);
            var box = new TextBox { Text = value, MaxLength = limit, Dock = DockStyle.Fill };
            layout.Controls.Add(box, 1, row);
            return box;
        }
        var name = Field("Usuário Roblox", original?.Username ?? "", 20);
        var alias = Field("Apelido local", original?.Alias ?? "", 80);
        var category = Field("Categoria", original?.Category ?? "Pessoal", 80);
        var userId = Field("ID público (opcional)", original?.UserId > 0 ? original.UserId.ToString(CultureInfo.InvariantCulture) : "", 19);
        var note = new Label { Text = "Somente dados públicos. Não informe credenciais.\nO cadastro não verifica identidade ou autentica a conta.", AutoSize = true };
        layout.Controls.Add(note, 0, layout.RowCount++);
        layout.SetColumnSpan(note, 2);
        var buttons = new FlowLayoutPanel { AutoSize = true };
        var save = new Button { Text = "Salvar", AutoSize = true };
        var cancel = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, AutoSize = true };
        save.Click += (_, _) =>
        {
            try
            {
                var candidate = new AccountEntry(original?.Id ?? Guid.NewGuid(), name.Text.Trim(), alias.Text.Trim(), category.Text.Trim(), Rules.ParseId(userId.Text, true));
                Rules.Validate(new LocalState(1, [candidate], candidate.Id, 0));
                Entry = candidate;
                DialogResult = DialogResult.OK;
            }
            catch (InvalidDataException) { MessageBox.Show(this, "Cadastro inválido. Use o nome público Roblox e um ID numérico positivo. Não informe cookies ou senhas.", "Manyblox"); }
        };
        buttons.Controls.AddRange([save, cancel]);
        layout.Controls.Add(buttons, 0, layout.RowCount++);
        layout.SetColumnSpan(buttons, 2);
        Controls.Add(layout);
        AcceptButton = save;
        CancelButton = cancel;
    }
}
