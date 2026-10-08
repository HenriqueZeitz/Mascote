# Mascote de mesa

Um bichinho que fica andando logo acima da barra de tarefas do Windows. Ele fala de vez em quando, avisa a hora e o clima,
recebe alertas dos colegas, guarda arquivos na mochila e topa ser atirado de um canhão.

Feito em C# com WPF (.NET 10). Só funciona no Windows.

## O que ele faz

- **Passeia sozinho**: anda, para, respira, pisca e solta uma frase de tempos em tempos. Funciona em vários monitores.
- **Clique e arraste**: leva o mascote para outro lugar. Solto de perto do chão, ele cai; solto do alto, desce de guarda-chuva.
- **Duplo clique**: pula e fala uma das frases do personagem.
- **Botão direito**: abre o menu com todo o resto.

| No menu | O que acontece |
|---|---|
| Enviar alerta | Manda uma mensagem para os mascotes dos colegas (todos ou só alguns). Quem recebe vê o mascote pular com um balão destacado. |
| Que horas são? | Ele anda até o relógio da barra de tarefas, olha e fala a hora. |
| Como está o tempo? | Fala a temperatura e o tempo na sua cidade. Quando chove lá fora, ganha uma nuvenzinha de chuva em cima da cabeça. |
| Canhão! | Você mira com o mouse, segura o botão para carregar a força e solta para atirar. Aparece uma cesta de basquete do outro lado da tela; acertar vale ponto e confete. |
| Roleta russa | 1 bala em 6. Se cair na bala, o mascote desmaia e **a tela do Windows é bloqueada** (igual a Win+L; nada é fechado nem perdido). |
| Mochila | Mostra o que está guardado. Para guardar, arraste arquivos ou pastas até o mascote: ele fica com uma **cópia**. |
| Falar algo / Ficar parado | Fala uma frase na hora / para de andar. |
| Trocar personagem / guarda-chuva | Escolhe entre os que estão na pasta `Dados`. |
| Editores | Criam e ajustam personagens e guarda-chuvas (veja abaixo). |

## Como rodar

Precisa do [SDK do .NET 10](https://dotnet.microsoft.com/download/dotnet/10.0).

```bash
dotnet run
```

Só uma instância roda por vez: abrir de novo com o mascote já na tela não faz nada.

## Como distribuir

Para mandar a alguém que não tem o .NET instalado, publique como arquivo único:

```bash
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=none -o publicar
```

O resultado é um único arquivo, `publicar\Mascote.exe` (cerca de 65 MB, já com o .NET dentro), e é só ele que precisa ser enviado.
Na primeira execução o mascote cria a pasta `Dados` ao lado do executável e instala o pato de exemplo.
Cada pessoa configura a própria cidade e a pasta de alertas pelo menu.
Como o executável não é assinado, o Windows pode mostrar o aviso do SmartScreen na primeira vez.

## Personagens

O mascote vem com um personagem, o **Pato**, que fica embutido no executável (pasta `Recursos\Pato` do projeto).
Ele é instalado quando não há nenhum personagem e é o usado enquanto você não escolhe outro em *Trocar personagem*.

Cada personagem é uma pasta em `Dados\personagens`, criada pelo **Editor de personagens**:

1. Abra um PNG com fundo transparente. O editor tenta detectar sozinho cabeça, olhos, braços, pés e cauda.
2. Confira e ajuste os membros. Arraste no desenho para marcar a área de um membro, em um de três formatos:
   retângulo, elipse ou **contorno livre** (você contorna o membro e o laço fecha ao soltar).
   O botão direito põe o ponto de giro, que é onde o membro se prende ao corpo.
3. Escreva as **frases do personagem**, uma por linha. Um personagem novo já vem com uma lista padrão.
4. Salve. Se for o personagem em uso, o mascote se atualiza sozinho em instantes.

A marcação não precisa ser exata: uma região de cor do desenho entra inteira no membro quando a maior parte dela está dentro da área marcada.

O **Editor de guarda-chuva** funciona do mesmo jeito, com dois pontos para marcar: onde o personagem segura e o topo, de onde ele balança.

## Alertas entre colegas

Os alertas passam por uma pasta compartilhada na rede, sem servidor. Todos apontam para a mesma pasta em
*Configurar alertas* e escolhem o nome que aparece para os outros. Quem está com o mascote aberto aparece na lista de destinatários
(a lista se atualiza a cada 20 segundos). Mensagens somem da pasta depois de um dia.

## Pasta `Dados`

É criada sozinha na primeira execução e guarda tudo o que é de quem está rodando o mascote. Não vai para o repositório.

| Caminho | Conteúdo |
|---|---|
| `personagens\<nome>\` | `rig.json` (membros, tamanho, frases), `original.png` e as camadas recortadas |
| `guarda-chuvas\<nome>\` | `guarda.json` e `imagem.png` |
| `mochila\` | Os arquivos guardados pelo mascote |
| `config.json` | Personagem e guarda-chuva em uso, cidade do clima, pasta e nome dos alertas |
| `erros.log` | Erros que aconteceram durante a animação (o mascote não fecha por causa deles) |

A pasta fica ao lado do executável. Rodando pelo `dotnet run`, o mascote usa a `Dados` da raiz do projeto, se ela existir;
se não, cria uma junto do executável compilado, em `bin\`. Se o executável estiver numa pasta onde não dá para gravar,
os dados vão para `%LOCALAPPDATA%\Mascote\Dados`.

## Organização do código

| Pasta | O que tem |
|---|---|
| `Principal\` | A janela do mascote: montagem, falas e mouse; o menu; e os estados da animação, um quadro a cada 33 ms |
| `Personagens\` | Leitura do `rig.json`, montagem do personagem em camadas, poses e gravação |
| `Rig\` | Processamento de imagem: carregar o PNG, detectar os membros e recortar cada um numa camada |
| `GuardaChuvas\` | O guarda-chuva padrão e os personalizados |
| `Editores\` | Editor de personagens e editor de guarda-chuva |
| `Alertas\`, `Clima\`, `Canhao\`, `Mochila\`, `Roleta\` | Uma pasta por funcionalidade, com o serviço, os desenhos em XAML e as janelas |
| `Interop\` | Chamadas diretas ao Windows: janela sempre no topo, monitores, relógio da barra de tarefas, ícones |
| `Nucleo\` | Caminhos, `config.json`, leitura e gravação de JSON e as frases padrão |
| `Recursos\Pato\` | O personagem padrão, embutido no executável |

O clima vem do [Open-Meteo](https://open-meteo.com/), que é gratuito e não pede cadastro.

## Opções de teste

Para experimentar a roleta sem bloquear a tela de verdade:

```bash
dotnet run -- --roleta-simular --roleta-forcar=bala
```

`--roleta-forcar` aceita `bala` ou `vazio`.
