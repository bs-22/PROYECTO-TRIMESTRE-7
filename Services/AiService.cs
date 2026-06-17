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

                // CORRECCIÓN 1: Modelo actualizado a gemini-1.5-flash
                string url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={_apiKey}";

                var payload = new
                {
                    contents = new[] {
                new { parts = new[] { new { text = promptFinal } } }
            }
                };

                string jsonContent = JsonConvert.SerializeObject(payload);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                // CORRECCIÓN 2 y 3: Lógica de reintentos (hasta 3 veces) para manejar el Error 503
                int maxReintentos = 3;
                for (int i = 0; i < maxReintentos; i++)
                {
                    HttpResponseMessage response = await client.PostAsync(url, content);

                    if (response.IsSuccessStatusCode)
                    {
                        string responseBody = await response.Content.ReadAsStringAsync();
                        JObject json = JObject.Parse(responseBody);
                        return json["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString() ?? "No hubo respuesta.";
                    }

                    // Si es error 503, esperamos 2 segundos y volvemos a intentar (si no es el último intento)
                    if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable && i < (maxReintentos - 1))
                    {
                        await Task.Delay(2000); // Esperar 2 segundos
                        continue; // Volver a intentar
                    }

                    // Si falla por otro motivo o se acaban los reintentos, enviamos mensaje amigable
                    if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
                    {
                        return "Samy está experimentando una alta demanda en este momento. Por favor, intenta de nuevo en unos segundos.";
                    }

                    // Para otros errores (400, 401, etc.)
                    return "Lo siento, tuve un problema técnico al conectarme con mis servidores. Intenta más tarde.";
                }

                return "No se pudo establecer conexión con el asistente.";
            }
            catch (Exception ex)
            {
                // Solo para depuración interna, no mostrar al usuario final
                System.Diagnostics.Debug.WriteLine("Error crítico: " + ex.Message);
                return "Ocurrió un error inesperado. Por favor, contacta a soporte.";
            }
        }
    }
}