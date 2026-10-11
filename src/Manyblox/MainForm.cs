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
    private readonly Dictionary<Guid, AccountBrowser> sessions = [];
    private readonly List<Button> selectionActions = [];
    private readonly Label selectedName = new() { AutoSize = true, Font = new Font("Segoe UI", 18, FontStyle.Bold), MaximumSize = new Size(430, 0) };
    private readonly Label selectedDetails = new() { AutoSize = true, MaximumSize = new Size(430, 0) };
    private readonly Label accountCount = new() { AutoSize = true };
    private AccountBrowser? loginWindow;
    private Button? playButton;
    private bool startingGame;
    private readonly MultiInstanceManager multi;
    private readonly CheckBox multiToggle = new() { Text = "Multi-Instance: Desativado (experimental)", AutoSize = true };
    private readonly Label clientCount = new() { AutoSize = true };
    private readonly ListView launchList = new() { Dock = DockStyle.Bottom, Height = 190, View = View.Details, FullRowSelect = true, MultiSelect = false };
    private readonly System.Windows.Forms.Timer monitorTimer = new() { Interval = 1000 };
    private bool updatingToggle;

    public MainForm(StateStore store, LocalState state)
    {
        this.store = store;
        this.state = state;
        multi = new MultiInstanceManager(diagnostic: new TechnicalLog(store.DirectoryPath).WriteDiagnostic);
        Text = "Manyblox 1.3.1 Beta 1 — Multi-Instance experimental";
        MinimumSize = new Size(880, 620);
        Size = new Size(1040, 720);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10);
        accounts.Columns.Add("Usuário", 170);
        accounts.Columns.Add("Apelido", 140);
        accounts.Columns.Add("Categoria", 120);
        accounts.Columns.Add("Sessão", 100);
        instances.Columns.Add("PID", 90);
        instances.Columns.Add("Iniciada em", 180);
        instances.Columns.Add("Janela", 550);
        launchList.Columns.Add("Conta solicitada", 145);
        launchList.Columns.Add("Conta no jogo", 170);
        launchList.Columns.Add("PID candidato", 105);
        launchList.Columns.Add("Estado", 135);
        launchList.Columns.Add("Diagnóstico", 330);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        var accountTab = new TabPage("Contas");
        var instanceTab = new TabPage("Instâncias");
        tabs.TabPages.AddRange([accountTab, instanceTab]);
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 90, Padding = new Padding(8), WrapContents = true };
        toolbar.Controls.Add(search);
        AddButton(toolbar, "+ Adicionar conta", Login);
        toolbar.Controls.Add(multiToggle);
        multiToggle.CheckedChanged += async (_, _) => await ToggleMultiAsync();
        var split = new SplitContainer { Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel2, Size = new Size(1000, 570), SplitterDistance = 530, Panel1MinSize = 300, Panel2MinSize = 400 };
        split.Panel1.Controls.Add(accounts);
        split.Panel1.Controls.Add(toolbar);
        var detail = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(20), BackColor = Color.White };
        detail.Controls.Add(accountCount);
        detail.Controls.Add(selectedName);
        detail.Controls.Add(selectedDetails);
        void Instruction(string text) => detail.Controls.Add(new Label { Text = text, AutoSize = true, MaximumSize = new Size(410, 0), Margin = new Padding(0, 20, 0, 8) });
        Instruction("1. Conecte sua conta\nAdicionar conta abre o login oficial. Depois de confirmar sua identidade, o app salva a sessão criptografada e fecha o navegador.");
        selectionActions.Add(AddButton(detail, "Entrar no Roblox", Login));
        Instruction("2. Selecione a conta e jogue\nDigite o Place ID, por exemplo 920587237. PLAY valida a sessão da conta selecionada e pede o lançamento ao site oficial.");
        place.Width = 260;
        place.AccessibleName = "Place ID do jogo";
        detail.Controls.Add(place);
        playButton = AddButton(detail, "PLAY", () => _ = PlayAsync());
        selectionActions.Add(playButton);
        Instruction("A senha não é salva. Multi-Instance experimental deve ser ativado antes do primeiro Roblox. Reservar os objetos não comprova compatibilidade nem contas simultâneas; confira cada identidade no jogo.");
        var edits = new FlowLayoutPanel { AutoSize = true, MaximumSize = new Size(430, 0) };
        selectionActions.Add(AddButton(edits, "Editar conta", () => Edit(true)));
        selectionActions.Add(AddButton(edits, "Remover conta", Remove));
        selectionActions.Add(AddButton(edits, "Ver perfil", Profile));
        detail.Controls.Add(edits);
        split.Panel2.Controls.Add(detail);
        accountTab.Controls.Add(split);
        Shown += (_, _) => { split.SplitterDistance = Math.Max(split.Panel1MinSize, split.Width - 465); RefreshAccounts(); };
        var instancebar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 50, Padding = new Padding(6) };
        AddButton(instancebar, "Atualizar instâncias", RefreshInstances);
        AddButton(instancebar, "Solicitar fechamento", CloseInstance);
        AddButton(instancebar, "Confirmar conta no jogo", ConfirmGameAccount);
        instancebar.Controls.Add(clientCount);
        instanceTab.Controls.Add(instances);
        instanceTab.Controls.Add(instancebar);
        instanceTab.Controls.Add(launchList);
        var instanceNotice = new Label { Dock = DockStyle.Bottom, Height = 65, Padding = new Padding(8), Text =
            "PID candidato é correlação temporal, não identidade verificada. Confirmação de conta depende da sua conferência dentro do jogo.\nMulti-Instance reserva objetos locais antes do primeiro cliente; não fecha handles do Roblox.\nDesligar/fechar o Manyblox libera seus próprios objetos; compatibilidade não comprovada." };
        instanceTab.Controls.Add(instanceNotice);
        Controls.Add(tabs);
        Controls.Add(status);
        search.TextChanged += (_, _) => RefreshAccounts();
        accounts.SelectedIndexChanged += (_, _) =>
        {
            if (!refreshing)
            {
                if (Selected() is AccountEntry a) Run(() => Commit(state with { SelectedId = a.Id }));
                UpdateSelection();
            }
        };
        accounts.DoubleClick += (_, _) => _ = PlayAsync();
        place.Text = state.LastPlaceId > 0 ? state.LastPlaceId.ToString(CultureInfo.InvariantCulture) : "";
        RefreshAccounts();
        status.Text = "Adicionar conta → login oficial → selecionar → PLAY.";
        monitorTimer.Tick += (_, _) => RefreshInstances();
        monitorTimer.Start();
        RefreshInstances();
        FormClosed += (_, _) =>
        {
            monitorTimer.Stop();
            foreach (var session in sessions.Values.ToArray()) session.Dispose();
            sessions.Clear();
            loginWindow?.Dispose();
            multi.Dispose();
        };
    }

    private Button AddButton(Control parent, string text, Action action)
    {
        var button = new Button { Text = text, AutoSize = true, Height = 32 };
        button.Click += (_, _) => Run(action);
        parent.Controls.Add(button);
        return button;
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
                var item = new ListViewItem([a.Username, a.Alias, a.Category, a.ProtectedSession.Length > 0 ? "Salva" : "Sem login"]) { Tag = a };
                accounts.Items.Add(item);
                if (a.Id == state.SelectedId) item.Selected = true;
            }
        }
        finally { accounts.EndUpdate(); refreshing = false; UpdateSelection(); }
    }

    private void UpdateSelection()
    {
        var selected = Selected();
        selectedName.Text = selected?.Username ?? "Escolha uma conta";
        selectedDetails.Text = selected is null ? "Use + Adicionar conta para começar. Depois selecione um cadastro na lista." :
            $"{selected.Alias} · {selected.Category}\n" + (selected.ProtectedSession.Length > 0 ? "Sessão criptografada salva · validada ao PLAY" : "Sem sessão salva. Entre no Roblox para conectar.");
        accountCount.Text = $"{state.Accounts.Count} conta(s) · {accounts.Items.Count} na lista";
        foreach (var button in selectionActions) button.Enabled = selected is not null;
        if (selectionActions.Count > 0) selectionActions[^1].Enabled = selected?.UserId > 0;
        if (playButton is not null) playButton.Enabled = !startingGame && !multi.HasPendingLaunch && !updatingToggle && selected?.ProtectedSession.Length > 0;
    }

    private void OpenSession(Uri page)
    {
        if (Selected() is not AccountEntry account) { status.Text = "Selecione uma conta na lista."; return; }
        if (!sessions.TryGetValue(account.Id, out var session))
        {
            session = new AccountBrowser(account, store.DirectoryPath, page);
            sessions.Add(account.Id, session);
            session.FormClosed += (_, _) => { sessions.Remove(account.Id); UpdateSelection(); };
            session.Show(this);
        }
        else { session.Navigate(page); session.Show(); session.Activate(); }
        UpdateSelection();
    }

    private void Edit(bool existing)
    {
        var current = existing ? Selected() : null;
        if (existing && current is null) { status.Text = "Selecione uma conta para editar."; return; }
        using var editor = new AccountEditor(current);
        if (editor.ShowDialog(this) != DialogResult.OK || editor.Entry is null) return;
        if (current is not null && current.Username != editor.Entry.Username && sessions.TryGetValue(current.Id, out var session)) session.Close();
        var list = state.Accounts.Where(a => a.Id != editor.Entry.Id).ToList();
        list.Add(editor.Entry);
        Commit(state with { Accounts = list, SelectedId = editor.Entry.Id });
        RefreshAccounts();
        status.Text = "Conta adicionada. Clique em Entrar no Roblox para autenticar no site oficial.";
    }

    private void Remove()
    {
        if (Selected() is not AccountEntry a) return;
        if (MessageBox.Show(this, $"Remover o cadastro local de {a.Username}? Isso não remove a conta Roblox.", "Manyblox", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        if (sessions.TryGetValue(a.Id, out var session)) session.Close();
        Commit(state with { Accounts = state.Accounts.Where(x => x.Id != a.Id).ToList(), SelectedId = null });
        RefreshAccounts();
    }

    private void Login()
    {
        if (loginWindow is not null && !loginWindow.IsDisposed) { loginWindow.Activate(); return; }
        loginWindow = new AccountBrowser(null, store.DirectoryPath, RobloxActions.LoginUri, confirmed =>
        {
            var existing = state.Accounts.FirstOrDefault(a => a.UserId == confirmed.UserId || a.Username.Equals(confirmed.Username, StringComparison.OrdinalIgnoreCase));
            var saved = existing is null ? confirmed : confirmed with { Id = existing.Id, Alias = existing.Alias, Category = existing.Category };
            Commit(state with { Accounts = state.Accounts.Where(a => a.Id != saved.Id).Append(saved).ToList(), SelectedId = saved.Id });
            if (sessions.TryGetValue(saved.Id, out var oldSession)) oldSession.Close();
            RefreshAccounts();
            status.Text = $"{saved.Username} conectada. Sessão salva com proteção Windows; selecione o jogo e clique em PLAY.";
        });
        loginWindow.FormClosed += (_, _) => loginWindow = null;
        loginWindow.Show(this);
    }

    private void Profile()
    {
        if (Selected() is not AccountEntry a || a.UserId == 0) { status.Text = "Selecione uma conta com ID público informado."; return; }
        OpenSession(RobloxActions.ProfileUri(a.UserId));
    }

    private async Task PlayAsync()
    {
        if (startingGame || multi.HasPendingLaunch) { status.Text = "Aguarde o lançamento pendente ou feche seu navegador para cancelar."; return; }
        var selected = Selected();
        if (selected is null || selected.ProtectedSession.Length == 0) { status.Text = "Conecte uma conta pelo login oficial antes de usar PLAY."; return; }
        startingGame = true;
        LaunchObservation? pending = null;
        AccountBrowser? pendingWindow = null;
        UpdateSelection();
        try
        {
            multi.Preflight();
            long id = Rules.ParseId(place.Text);
            using var api = new RobloxSessionApi();
            var identity = await api.ValidateAccountAsync(selected);
            if (IsDisposed) return;
            // Never launch a different identity under a saved account's label.
            if (identity.Id != selected.UserId) throw new SessionExpiredException();
            if (!state.Accounts.Any(a => a.Id == selected.Id && a.ProtectedSession == selected.ProtectedSession)) return;
            multi.Poll();
            if (!multi.MonitoringAvailable) throw new InvalidOperationException("PROCESS_SNAPSHOT_FAILED");
            if (sessions.TryGetValue(selected.Id, out var previous)) previous.Close();
            var launch = multi.BeginLaunch(selected.Username);
            pending = launch;
            var window = new AccountBrowser(selected, store.DirectoryPath, RobloxActions.GameUri(id), playPlaceId: id);
            pendingWindow = window;
            sessions[selected.Id] = window;
            window.FormClosed += (_, _) => { sessions.Remove(selected.Id); multi.BrowserClosed(launch); UpdateSelection(); };
            window.ClientLaunchStarting += (_, _) => multi.BeforeHandoff(launch);
            window.ClientLaunchFailed += (_, _) => multi.Fail(launch, "HANDOFF_FAILED");
            window.ClientLaunchRequested += (_, _) => status.Text = $"Pedido enviado para {selected.Username}; aguardando novo processo. A conta no jogo não foi verificada.";
            Commit(state with { LastPlaceId = id });
            window.Show(this);
            status.Text = $"Sessão de {selected.Username} confirmada. Preparando lançamento pelo site oficial…";
        }
        catch (SessionExpiredException) { if (!IsDisposed) status.Text = "A sessão expirou ou não corresponde à conta. Clique em Entrar no Roblox para reconectar."; }
        catch (InvalidDataException) { if (!IsDisposed) status.Text = "Informe um Place ID numérico positivo ou reconecte a conta se a sessão não puder ser aberta."; }
        catch (InvalidOperationException e) when (e.Message is "MULTI_NOT_PREPARED" or "RESERVATION_LOST" or "ACTIVATION_PENDING")
        {
            if (pending is not null) multi.Fail(pending, "LAUNCH_FAILED");
            pendingWindow?.Close();
            if (!IsDisposed) status.Text = "Lançamento bloqueado: Multi-Instance não preparado. Feche manualmente os clientes e ative o modo antes do primeiro PLAY; nenhum cliente foi encerrado pelo app.";
        }
        catch
        {
            if (pending?.Phase == LaunchPhase.PreparingBrowser) multi.Fail(pending, "LAUNCH_FAILED");
            pendingWindow?.Close();
            if (!IsDisposed) status.Text = "Não foi possível validar ou iniciar a conta. Verifique sua conexão e tente novamente.";
        }
        finally { startingGame = false; if (!IsDisposed) UpdateSelection(); }
    }

    private void RefreshInstances()
    {
        multi.Poll();
        if (!multi.MonitoringAvailable) { clientCount.Text = "Monitor indisponível"; return; }
        int? selectedPid = instances.SelectedItems.Count == 1 && instances.SelectedItems[0].Tag is InstanceEntry selected ? selected.Pid : null;
        instances.BeginUpdate();
        instances.Items.Clear();
        foreach (var client in multi.Clients)
        {
            var entry = new InstanceEntry(client.Pid, client.Started.ToLocalTime(), client.Title);
            var item = new ListViewItem([entry.Pid.ToString(CultureInfo.InvariantCulture), entry.Started.ToString("g"), entry.Title]) { Tag = entry };
            instances.Items.Add(item);
            if (entry.Pid == selectedPid) item.Selected = true;
        }
        instances.EndUpdate();
        clientCount.Text = $"{multi.Clients.Count} processo(s) Roblox";
        Guid? selectedLaunch = launchList.SelectedItems.Count == 1 && launchList.SelectedItems[0].Tag is LaunchObservation observation ? observation.Id : null;
        launchList.BeginUpdate();
        launchList.Items.Clear();
        foreach (var launch in multi.Launches)
        {
            var phase = launch.Phase switch
            {
                LaunchPhase.PreparingBrowser => "Preparando navegador",
                LaunchPhase.AwaitingProcess => "Aguardando cliente",
                LaunchPhase.ProcessObserved => "Processo observado",
                LaunchPhase.StableProcess => "Processo estável",
                LaunchPhase.Ended => "Encerrado",
                LaunchPhase.Failed => "Erro",
                LaunchPhase.TimedOut => "Tempo esgotado",
                _ => "Ambíguo"
            };
            var item = new ListViewItem([launch.RequestedAccount, launch.AccountConfirmedByUser ? "Confirmada pelo usuário" : "Não verificada", launch.Client?.Pid.ToString(CultureInfo.InvariantCulture) ?? "—", phase, launch.Code]) { Tag = launch };
            launchList.Items.Add(item);
            if (launch.Id == selectedLaunch) item.Selected = true;
        }
        launchList.EndUpdate();
        UpdateSelection();
    }

    private async Task ToggleMultiAsync()
    {
        if (updatingToggle) return;
        updatingToggle = true;
        multiToggle.Enabled = false;
        try
        {
            if (multiToggle.Checked)
            {
                await multi.EnableAsync();
                if (!IsDisposed) status.Text = "Objetos reservados. Modo experimental; não comprova vários clientes funcionando.";
            }
            else multi.Disable();
        }
        catch
        {
            if (!IsDisposed) status.Text = "Não foi possível mudar Multi-Instance. Feche os clientes/launchers manualmente e ative antes do primeiro Roblox; objetos existentes não serão alterados.";
        }
        finally
        {
            if (!IsDisposed)
            {
                multiToggle.Checked = multi.Enabled;
                multiToggle.Text = multi.Enabled ? "Multi-Instance: Ativado (experimental)" : "Multi-Instance: Desativado (experimental)";
                multiToggle.Enabled = true;
            }
            updatingToggle = false;
        }
    }

    private void ConfirmGameAccount()
    {
        if (launchList.SelectedItems.Count != 1 || launchList.SelectedItems[0].Tag is not LaunchObservation launch || launch.Client is null) return;
        if (MessageBox.Show(this, $"Você conferiu dentro do jogo do PID {launch.Client.Pid} que o usuário é {launch.RequestedAccount}? Esta é uma confirmação manual, não uma verificação automática.", "Confirmar identidade", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        multi.ConfirmAccountByUser(launch);
        RefreshInstances();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { monitorTimer.Dispose(); multi.Dispose(); }
        base.Dispose(disposing);
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
        if (original?.ProtectedSession.Length > 0) { name.ReadOnly = true; userId.ReadOnly = true; }
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
                var candidate = new AccountEntry(original?.Id ?? Guid.NewGuid(), name.Text.Trim(), alias.Text.Trim(), category.Text.Trim(), Rules.ParseId(userId.Text, true), original?.ProtectedSession ?? "");
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
