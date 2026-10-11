> Histórico: este documento registra uma revisão anterior. Para v1.3.1-beta.1, consulte README.md, SECURITY.md e MULTI_INSTANCE_ORIGINAL_COMPARISON.md. O usuário relatou duas instâncias funcionando em 1.3.1; não é certificação independente.

# Login interno — revisão 1.2.0 local

> Documento do fluxo de sessão introduzido em 1.2. Na revisão 1.3, Multi-Instance experimental foi acrescentado, e o prazo da API passou a cobrir leitura do corpo. As afirmações abaixo de ausência de bypass e os resultados de teste 1.2 são históricos; consulte `MULTI_INSTANCE_IMPLEMENTATION.md` para a implementação atual e sua tentativa de contornar exclusividade.

Esta candidata substitui a 1.1.0. Não é uma release validada ou um produto oficial Roblox.

## Fluxo

1. **Adicionar conta** abre `https://www.roblox.com/login` em um perfil WebView2 privado novo. Não exige cadastrar o nome antes.
2. Depois do login, o app lê somente a sessão `.ROBLOSECURITY` desse navegador interno e consulta `GET https://users.roblox.com/v1/users/authenticated`. Página aberta ou cookie presente não basta: o servidor precisa confirmar a identidade. Só então o cadastro é salvo e o navegador fechado. Cancelar antes disso não adiciona uma conta.
3. A sessão é protegida com **DPAPI CurrentUser**, separadamente dos metadados também criptografados. Não se salva senha. Login repetido da mesma identidade atualiza a sessão e preserva apelido/categoria.
4. Selecione a conta, informe o Place ID e clique em **PLAY**. O app valida novamente o cookie e compara o ID autenticado ao salvo antes de qualquer lançamento. Sessão expirada ou identidade diferente impede o lançamento.
5. O app restaura a sessão em um perfil privado isolado, abre a página oficial do jogo e tenta clicar em Jogar com um script local fixo que só procura esse botão. O site gera a solicitação de abertura; o app a encaminha ao protocolo Roblox registrado no Windows, sem fabricar tickets. Depois de solicitar o lançamento, fecha o navegador. Se layout, CAPTCHA ou outro requisito impedir a ação, deixa a página para ação manual e informa que não completou o lançamento.
6. Remover a conta elimina a sessão da base local. Não revoga sessões no servidor nem desconecta um cliente aberto; use os controles oficiais Roblox para revogação.

Cadastros antigos sem sessão aparecem como **Sem login** e não podem usar PLAY. Reconecte-os. Os executáveis antigos recusam os novos campos em vez de sobrescrever a base.

## Duas instâncias e regras

Não há bypass de mutex/evento, injeção, patch ou alteração de flags/executável Roblox. Havendo janela Roblox aberta, PLAY avisa que o cliente pode substituir a sessão atual e pede confirmação (padrão: Não). **Duas contas jogando simultaneamente no mesmo computador não estão validadas nem garantidas.** Essa exigência permanece pendente sob a prioridade de não contornar restrições do cliente.

O código não altera gameplay nem faz evasão de antivírus/anticheat. Isso não significa aprovação oficial, ausência garantida de banimento ou zero alertas de antivírus. A [troca oficial de contas](https://en.help.roblox.com/hc/en-us/articles/21037888001044-Account-Switching) documenta troca na web; não foi considerada prova de suporte a dois clientes simultâneos.

## Dados e confiança

- SDK **Microsoft.Web.WebView2 1.0.4258.31**, assinatura Microsoft/NuGet verificada. Mantenha o runtime Evergreen atualizado. Não reutiliza CEF/launcher/DLLs do ZIP original.
- Perfis privados em diretórios novos dentro de `%LOCALAPPDATA%\Manyblox\WebSessions`, com DACL restrita ao usuário Windows e SYSTEM. Nunca lê perfis pessoais de navegadores.
- A sessão é uma credencial de acesso. A leitura do cookie interno foi autorizada pelo pedido de salvar autenticação. Não há importação/exportação de cookies, logs de credenciais ou captura de senha. Strings de sessão/ticket precisam existir temporariamente no runtime/CLR: não se promete apagamento completo nem proteção contra processo malicioso já executando como o mesmo usuário.
- A consulta de identidade envia o cookie somente ao endpoint HTTPS fixo acima, sem redirects, query com credenciais ou alteração de TLS. Resposta limitada a 64 KiB; corpos e headers não integram erros. A base tem limite de 4 MiB; excedê-lo preserva o arquivo anterior.
- Navegação principal/popups limitados a HTTPS, porta padrão, `roblox.com`/`www.roblox.com`, sem credenciais na URL. Subrecursos, scripts, APIs, CDN e verificadores do site são carregados pelo navegador. Não há allowlist completa de subrecursos nem promessa de ausência de telemetria do site/runtime.
- Sem objetos nativos/mensagens web expostos à página, DevTools, senhas salvas, autofill, downloads ou câmera/microfone/localização. Erro de certificado cancela conexão; nenhuma flag reduz TLS, sandbox ou segurança web.
- PLAY executa somente um script constante de clique, sem ler formulários ou executar código fornecido pelo usuário. Autoriza um encaminhamento por até 30 segundos, apenas na página do Place ID selecionado e com origem oficial. Fora desse fluxo, protocolos exigem gesto humano e confirmação. Protocolos aceitos: `roblox`/`roblox-player`; controles, aspas e tamanho excessivo são rejeitados.
- O handler registrado no Windows recebe a solicitação oficial, possivelmente contendo um ticket temporário. O ticket não é persistido ou incluído em erros. A assinatura do handler não é verificada pelo app; instale Roblox pelo canal oficial. ShellExecute retornar não comprova que o jogo iniciou ou qual conta o cliente exibiu.
- Cookies rotacionados pelo site durante PLAY não são regravados automaticamente. Sessões podem expirar ou ser revogadas, exigindo novo login. Pastas de suporte podem permanecer; modo privado não garante ausência de artefatos em memória, paginação, cache ou crash dumps.

## Validação

- 21 testes locais/sintéticos: DPAPI, persistência após reiniciar, ausência do segredo sintético no disco/base decifrada, capacidade/DACL, destino fixo de identidade, rejeição de redirects/respostas excessivas/identidade diferente/injeção em headers e protocolos, política de navegação e renderização.
- Smoke opcional `--webview-smoke`: abre só o formulário público, confere perfil privado/proteções, salva captura e confirma que fechar sem login não salva conta. Não fornece credenciais.
- Varredura local Microsoft Defender no build: nenhuma detecção correspondente observada. Esse resultado vale para a verificação local registrada em `audit/defender-check-1.2.0.json`, não para todos os antivírus ou futuras versões.
- **NÃO TESTADO:** login real com senha/2FA/CAPTCHA/passkey, captura/persistência de sessão real, clique e lançamento real pelo PLAY, conta dentro do cliente, duas contas simultâneas, tráfego completo e análise forense de artefatos. Executável próprio não assinado e sem avaliação VirusTotal/outros antivírus. Não há garantia de compatibilidade futura, ausência de banimento ou de falsos positivos.

## Referências oficiais

- [Identidade autenticada Roblox](https://create.roblox.com/docs/cloud/reference/domains/users)
- [Segurança WebView2](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/security)
- [Perfis](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/multi-profile-support)
- [Dados do navegador](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/user-data-folder)
- [Pacote Microsoft](https://www.nuget.org/packages/Microsoft.Web.WebView2/1.0.4258.31)
