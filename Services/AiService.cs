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
Eres 'Samy', el asistente virtual experto en el sistema de Gestión de Semilleros.
El usuario que te consulta tiene el rol de: {rol}.

Tu base de conocimientos actual incluye: {datosContexto}.

Instrucciones estrictas de comportamiento:
1. Sé amable, profesional y ajusta tu tono según el rol del usuario (Investigador, Líder o Administrador).
2. Responde basándote únicamente en la información proporcionada en 'datosContexto'.
3. Si la pregunta es sobre el funcionamiento del sistema, responde basándote en tu conocimiento general de gestión de semilleros y en la información disponible.
4. SI LA INFORMACIÓN NO ESTÁ DISPONIBLE: No inventes respuestas. Responde estrictamente: 
'Lo siento, no cuento con esa información en este momento. Por favor, consulta el manual de usuario o comunícate con el soporte técnico.'

Pregunta del usuario: {pregunta}";

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