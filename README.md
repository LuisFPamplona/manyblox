# Manyblox

Reconstrução independente em C# do conceito de gerenciamento local de contas Roblox, com foco em reduzir acesso a credenciais e superfície de execução. Não é um produto oficial Roblox.

Cadastre **somente o nome público**, apelido, categoria e, opcionalmente, ID público da conta. Os cadastros não comprovam identidade nem autenticam sessões. Use **Login oficial** e faça a troca manual no navegador/cliente Roblox. Informe o Place ID e escolha **Iniciar Roblox**; o cliente instalado usa sua sessão atual, independentemente do cadastro selecionado. A opção **Abrir página do jogo** é a alternativa caso o protocolo não esteja disponível.

Em **Instâncias**, atualize a lista e solicite o fechamento de uma janela específica. Não há encerramento forçado, associação automática de janela a conta, injeção ou bypass de instância única.

Dados em `%LOCALAPPDATA%\Manyblox\accounts.dpapi`, criptografados para o usuário do Windows. `session.lock` impede duas janelas de escrever simultaneamente. A busca é transitória; seleção e último Place ID persistem. Não há cookies, senhas, telemetria, atualização automática ou servidor local. Não há exportação nem importação de arquivos antigos.

Consulte `BUILD.md`, `SECURITY_AUDIT.md` e `COMPARISON.md`. Os originais em `audit/original` servem apenas como evidência: **não os execute nem compile**. Eles não integram os ZIPs de distribuição limpos.

O executável não é assinado digitalmente. Os limites dos testes e da proteção estão no relatório. Não há garantia de ausência de malware ou segurança absoluta.
