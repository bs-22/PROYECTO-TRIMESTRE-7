using GenerativeAI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using GestionSemillero1.Models;

namespace GestionSemillero1.Services
{
    public class AiService
    {
        private readonly string _apiKey = ConfigurationManager.AppSettings["GeminiApiKey"];
        private static readonly HttpClient client = new HttpClient();

        public async Task<string> GenerarRespuesta(string pregunta, string datosContexto, string rol)
        {
            try
            {
                // Definimos un prompt robusto que instruye a la IA sobre su rol y límites
                string promptFinal = $@"
Eres 'Samy', el asistente virtual experto en la plataforma 'SemiPlan'.
ROL DEL USUARIO: {rol}
DATOS DEL USUARIO: {datosContexto}

TU BASE DE CONOCIMIENTO (Definiciones):
- Semillero: Grupo de investigación donde se desarrollan proyectos.
- Proyecto: Iniciativa de investigación con fases y actividades.
- Reunión: Espacio de coordinación y seguimiento.
- Evento: Actividad académica o administrativa del grupo.
- Reporte: Informe detallado de actividades.

INSTRUCCIONES:
1. Si te preguntan por definiciones, usa la 'BASE DE CONOCIMIENTO' arriba definida.
2. Si te preguntan por datos específicos (ej: 'cuántos investigadores tengo'), usa exclusivamente los 'DATOS DEL USUARIO' proporcionados.
3. Si el usuario pregunta algo que no puedes responder, se amable y sugiere consultar el manual.
4. Ajusta tu tono: {rol}.

Pregunta: {pregunta}";

                string url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-3.5-flash:generateContent?key={_apiKey}";

                var payload = new
                {
                    contents = new[] {
                        new { parts = new[] { new { text = promptFinal } } }
                    }
                };

                string jsonContent = JsonConvert.SerializeObject(payload);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                HttpResponseMessage response = await client.PostAsync(url, content);
                string responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return "Error técnico en la API: " + responseBody;

                JObject json = JObject.Parse(responseBody);
                return json["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString() ?? "No hubo respuesta.";
            }
            catch (Exception ex)
            {
                return "Error crítico: " + ex.Message;
            }
        }
    }
}