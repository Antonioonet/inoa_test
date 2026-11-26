# Stock Quote Alert

## Como funciona

O sistema funciona da seguinte forma:

- Cada vez que é chamada uma adição nova de uma nova ação, ela insere a empresa no banco de dados (se o BD não existir, ele é criado).
- Se a empresa já existir no monitoramento, ela é atualizada.
- O monitoramento da ação é feito por um programa **monitor** (que, se não existir, é criado), que fica encarregado de, de tempos em tempos, monitorar o preço das ações do banco de dados, fazer o request e enviar para o e-mail destinatário um aviso.
- As informações sobre o e-mail de destino e a configuração do SMTP ficam no arquivo de configuração (`appsettings.json`).
- Também foram adicionados scripts para criar executáveis, contidos em `cria_exe.sh`.

## Evolução da modelagem

A modelagem do sistema passou por três versões:

### 1) Uma thread por chamada

A cada chamada, o programa ficava rodando em background como uma thread, acompanhando a ação e aquele range específico.

- Problema: se chamar muitas vezes, fica criando muitas threads; pouco escalável e um saco de organizar.

### 2) CSV

Usar um CSV para armazenar as ações e intervalos, com o `Program.cs` sendo usado para inserir e o `Monitor.cs` para fazer os requests.

- Vantagem: fácil de implementar e não precisa de biblioteca extra.
- Desvantagem: lidar com ações duplicadas não é fácil, e ficar verificando se uma ação já foi usada não escala.

### 3) Banco de dados (versão atual)

Usar a ideia de um processo para inserir e outro para monitorar, só que com um banco de dados para resolver os problemas de escalabilidade.

## Uso de I.A

Dada a minha nao familiariadade com a linguagem, usei a ferramenta claude code
para dar suporte em detalhes de implementacao e sintaxe (e. g. comocriar um projeto e C#,como criar um processo em paralelos em C#, como ler do argumentos do teminal, 
como mandar um e-mail pelo codigo e que biblioteca usar). Tambem o usei, ao final, para "polir" o codigo, organizando e comentando partes faltantes (incluindo a formatacao desse READE.ME).
