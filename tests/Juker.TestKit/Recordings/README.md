# Recordings

Respostas gravadas de APIs externas servidas pelo `FakeHttpServer` (WireMock.Net), conforme o ADR-0012, regra 8.

- Cada arquivo `*.json` é um mapeamento estático do WireMock (`Request` + `Response`), copiado para a saída de
  quem referencia o `Juker.TestKit` e carregado pelo `FakeHttpServer` na construção.
- As respostas da YouTube Data API são regravadas por `tools/recordings/refresh-youtube.sh`, que exige
  `YOUTUBE_API_KEY`. A chave nunca é gravada. Ela só vai na requisição real e não entra nos matchers do
  mapeamento, que casam só os parâmetros listados no script. O script falha, sem gravar nada, se a chave
  aparecer em alguma resposta.
- Nenhum teste chama a API real. Requisição sem gravação correspondente recebe 404.

As primeiras gravações chegam com a SPEC-0014 (busca no catálogo).
