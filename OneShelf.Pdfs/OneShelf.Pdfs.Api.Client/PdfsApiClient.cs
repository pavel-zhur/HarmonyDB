using System.Text.RegularExpressions;
using HarmonyDB.Common;
using HarmonyDB.Common.Transposition;
using Microsoft.Extensions.Options;

namespace OneShelf.Pdfs.Api.Client;

public class PdfsApiClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly PdfsApiClientOptions _options;

    public PdfsApiClient(IHttpClientFactory httpClientFactory, IOptions<PdfsApiClientOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
    }

    public string GenerateFinalHtml(string html, string artist, string title, bool twoColumns, int transpose, string shortSourceName, NoteAlteration? alteration)
    {
        html = $@"

<html>
    <head>
        <style>

@page {{

    size: A4;
    margin: 16mm;

}}

  .two {{
    column-count: 2;
    -webkit-column-count: 2;
    -moz-column-count: 2;
  }}

h4.song-title {{
    font-family: Open Sans,-apple-system,Roboto,Helvetica neue,Helvetica,sans-serif;
    font-size: 150%;
}}

pre {{
    line-height: 21px;
    font-family: SFMono-Regular,Menlo,Monaco,Consolas,""Liberation Mono"",""Courier New"",monospace;
    font-size: 14px;
}}

div.chords-block div.pre-variable-width {{
  white-space: pre-wrap;
  font-family: Tahoma, Roboto, Arial, Helvetica, sans-serif;
}}

div.chords-block pre {{
  white-space: pre-wrap;
  padding-bottom: 6px;
}}

div.chords-block .chord {{
  background-color: white;
  font-weight: bold;
  padding: 0 3px;
  cursor: pointer;
  text-wrap: nowrap;
  white-space: nowrap;
}}

div.chords-block div.bold-div {{
  font-weight: bold;
}}

div.chords-block span.chords-line {{
  line-height: inherit;
}}

div.chords-block div.chords-line {{
  background: white;
}}

div.chords-block div.chords-double-line {{
  margin-bottom: 2px;
  page-break-inside: avoid;
}}

div.chords-block span.chords-double-line {{
    line-height: 36px;
    position: relative;
    top: 8px;
    display: inline-block;
}}

div.chords-block span.flying {{
  position: absolute;
}}

div.chords-block span.flying2 {{
  position: relative;
  top: -17px;
}}

        </style>
    </head>
    <body>

<h4 class='song-title'>
    {artist} &minus; {title} <span style='font-size: 70%; font-weight: normal;'>({shortSourceName}{(transpose == 0 ? "" : $", {transpose.Transposition()}")}{(alteration.HasValue ? alteration == NoteAlteration.Flat ? ", b" : ", #" : "")})</span>
</h4>

        <div class=""chords-block {(twoColumns ? "two" : "")}"">
            {html}
        </div>
    </body>
</html>

";
        return html;
    }

    public async Task<(byte[] pdf, int pageCount)> Convert(string html)
    {
        using var client = _httpClientFactory.CreateClient();
        using var response = await client.PostAsync(_options.Endpoint, new StringContent(html));
        response.EnsureSuccessStatusCode();
        var pageCount = int.Parse(Regex.Match(response.Content.Headers.ContentDisposition!.FileName!, "(\\d+)\\.pdf").Groups[1].Value);
        return (await response.Content.ReadAsByteArrayAsync(), pageCount);
    }
}