# Plano e decisões

1. Inventariar o ZIP sem executar seus binários ou builds. Preservar evidências em `audit/original`, registrar SHA-256 e superfícies de rede/execução.
2. Prioridade crítica: retirar Nexus/Lua remoto, servidores locais, exposição de cookies, navegador inseguro e atualizador sem verificação. Não compilar o projeto original, que copia um launcher pré-compilado no pós-build.
3. Reconstruir em C#/.NET 10 Windows Forms, sem pacotes NuGet de terceiros, sem credenciais e sem scripts/customização executável. Todos os módulos originais ficam fora da nova árvore de compilação.
4. Preservar cadastro local, edição, remoção, apelidos/categorias, busca e seleção persistente; abrir login/perfil/jogo no navegador oficial e iniciar o cliente por protocolo registrado. Não prometer que o cadastro selecionado corresponde à sessão autenticada.
5. Proteger metadados com DPAPI CurrentUser, DACL do usuário/SYSTEM, gravação atômica, bloqueio entre instâncias e falha fechada em corrupção. Nenhuma migração de segredos antigos.
6. Enumerar janelas Roblox por APIs gerenciadas; fechamento normal com confirmação e validação de PID/horário. Retirar leitura de command lines, logs, utilitários de handles, alterações do cliente e bypass de mutex.
7. Compilar apenas o código novo, rodar testes sintéticos e documentar limites. SDK obtido da Microsoft, hash SHA-512 conferido antes de qualquer execução. Não instalar ferramentas globalmente.

A ausência de um fluxo oficial de troca automática de sessão demonstrado para esse aplicativo impede preservar essa função sem voltar a manipular cookies. OAuth oficial existe para identidade/recursos Open Cloud, mas requer registro de aplicativo e não foi demonstrado aqui como mecanismo de login do cliente desktop. Portanto, a alternativa implementada é login manual; não é equivalência funcional completa ao original.
