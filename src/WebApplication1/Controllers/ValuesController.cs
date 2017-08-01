using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Dynamic;
using System.Net.Http;
using Newtonsoft.Json;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WebApplication1.Model;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.Extensions.Options;
using WebApplication1.Configuration;

namespace WebApplication1.Controllers
{
    [Route("api/contacts")]
    public class ValuesController : Controller
    {
        private IConfiguration Configuration;
        private string UserName;
        private string URL;
        private string Password;
        private string AuthenticationHeader;
        private string RedirectURL;
        private ILogger<ValuesController> _logger;

        public ValuesController(IOptions<Credentials> credentials, ILogger<ValuesController> logger)
        {
            UserName = credentials.Value.UserName;
            Password = credentials.Value.Password;
            URL = credentials.Value.URL;
            RedirectURL = credentials.Value.RedirectURL;
            AuthenticationHeader = Convert.ToBase64String(Encoding.ASCII.GetBytes(String.Format("{0}:{1}", UserName, Password)));
            _logger = logger;
        }

        private string GetBase64String(string login)
        {
            return Convert.ToBase64String(Encoding.ASCII.GetBytes("p_userid=" + login));
        }


        // GET api/values
        [HttpGet]
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }

        // GET api/values/5
        [HttpGet("{id}", Name = "GetContact")]
        public async Task<IActionResult> Get(int id)
        {
            string getURL =  $"{URL}/{id}";

            System.Net.Http.HttpClient client = new System.Net.Http.HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", "Basic " + AuthenticationHeader);

            dynamic result1 = await client.GetAsync(getURL).Result.Content.ReadAsStringAsync();

            var result2 = JsonConvert.DeserializeObject<dynamic>(result1);

            return new JsonResult(result2);
        }

        // POST api/values
        [HttpPost]
        public async Task<IActionResult> Post([FromBody]PostContactDTO contactDTO)
        {

            if (contactDTO == null)
            {
                return new BadRequestResult();
            }

            string contactId;
            var existingContact = FindCurrentContact(contactDTO.Login, out contactId);

            if (existingContact)
            {
                var putResult = await Put(contactDTO);
                if (putResult is StatusCodeResult && ((StatusCodeResult)putResult).StatusCode == 200)
                {
                    HttpContext.Response.Headers.Add("X-Redirect-URL", string.Format(RedirectURL, GetBase64String(contactDTO.Login)));
                }
                return putResult;
            }

            if (!ModelState.IsValid)
            {
                return new BadRequestObjectResult(ModelState);
            }



            dynamic contact = new ExpandoObject();
            contact.name = new ExpandoObject();
            contact.name.first = contactDTO.FirstName;
            contact.name.last = contactDTO.LastName;
            contact.login = contactDTO.Login;
            contact.emails = new ExpandoObject();
            contact.emails.address = contactDTO.EmailAddress;
            contact.emails.addressType = new ExpandoObject();
            contact.emails.addressType.id = 0;
            contact.serviceSettings = new ExpandoObject();
            contact.serviceSettings.sLAInstances = new List<dynamic>();

            foreach(var sla in contactDTO.SLAs)
            {
                dynamic sLaInstance1 = new ExpandoObject();

                sLaInstance1.activeDate = sla.ActiveDate.ToString("yyyy-MM-dd");
                sLaInstance1.nameOfSLA = new ExpandoObject();
                sLaInstance1.nameOfSLA.lookupName = sla.Name;
                sLaInstance1.stateOfSLA = new ExpandoObject();
                sLaInstance1.stateOfSLA.lookupName = sla.State;
                contact.serviceSettings.sLAInstances.Add(sLaInstance1);
            }


            System.Net.Http.HttpClient client = new System.Net.Http.HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", "Basic " + AuthenticationHeader);

            var result = client.PostAsync(URL, new StringContent(JsonConvert.SerializeObject(contact))).Result;

            var strResult =  result.Content.ReadAsStringAsync().Result;

            if (result.IsSuccessStatusCode)
            {
                var location = result.Headers.Where(h => h.Key == "Location").First().Value.First();

                dynamic result1 = client.GetAsync(location).Result.Content.ReadAsStringAsync().Result;

                var result2 = JsonConvert.DeserializeObject<dynamic>(result1);

                _logger.LogInformation("Successfully completed post");

                Response.Headers.Add("X-Redirect-URL", string.Format(RedirectURL, GetBase64String(contactDTO.Login)));

                return CreatedAtRoute("GetContact", new { id = result2.id }, result2);
            }
            else
            {
                _logger.LogError($"Request failed for login {contactDTO.Login}. Reason {strResult}");
                return BadRequest();
            }
        }

        // PUT api/values/5
        [HttpPut()] 
        public async Task<IActionResult> Put([FromBody]dynamic contactDTO)
        {
            string contactId;

            if (contactDTO == null)
            {
                return new BadRequestResult();
            }

            if (contactDTO.Login == null)
            {
                ModelState.AddModelError("Login", "Login is mandatory");
                return await Task.FromResult(new BadRequestObjectResult(ModelState));
            }

            var existingContact = FindCurrentContact(contactDTO.Login.ToString(), out contactId);
          
            if (!existingContact)
            {
                return new NotFoundResult();
            }

            string json = JsonConvert.SerializeObject(contactDTO); // suppose `dynamicObject` is your input
            Dictionary<string, dynamic> contactDictionary = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(json);

            dynamic contact = new ExpandoObject();

            if (contactDictionary.ContainsKey("FirstName"))
            {
                contact.name = new ExpandoObject();
                contact.name.first = contactDTO.FirstName;
            }

            if (contactDictionary.ContainsKey("LastName"))
            {
                contact.name = contact.name ?? new ExpandoObject();
                contact.name.last = contactDTO.LastName;
            }

            if (contactDictionary.ContainsKey("EmailAddress"))
            {
                contact.emails = new ExpandoObject();
                contact.emails.address = contactDTO.EmailAddress;
                contact.emails.addressType = new ExpandoObject();
                contact.emails.addressType.id = 0;
            }

            if (contactDictionary.ContainsKey("SLAs"))
            {
                contact.serviceSettings = new ExpandoObject();
                contact.serviceSettings.sLAInstances = new List<dynamic>();

                foreach (var sla in contactDTO.SLAs)
                {
                    dynamic sLaInstance1 = new ExpandoObject();

                    sLaInstance1.activeDate = sla.ActiveDate.ToString("yyyy-MM-dd");
                    sLaInstance1.nameOfSLA = new ExpandoObject();
                    sLaInstance1.nameOfSLA.lookupName = sla.Name;
                    sLaInstance1.stateOfSLA = new ExpandoObject();
                    sLaInstance1.stateOfSLA.lookupName = sla.State;
                    contact.serviceSettings.sLAInstances.Add(sLaInstance1);
                }
            }         


            string putURL = $"{URL}/{ contactId}";

            System.Net.Http.HttpClient client = new System.Net.Http.HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", "Basic " + AuthenticationHeader);

            client.DefaultRequestHeaders.Add("X-HTTP-Method-Override", "PATCH");

            HttpResponseMessage response = new HttpResponseMessage();
            response = await client.PostAsync(putURL, new StringContent(JsonConvert.SerializeObject(contact)));

            if (response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode);
            }
            {
                var strResult = response.Content.ReadAsStringAsync().Result;
                _logger.LogError($"Request failed for login {contactDTO.Login}. Reason {strResult}");
                return new BadRequestResult();
            }
        }

        // DELETE api/values/5
        [HttpDelete]
        public async Task<IActionResult> Delete([FromBody]DeactivateDTO contactDTO)
        {
                if (!ModelState.IsValid)
            {
                return new BadRequestObjectResult(ModelState);
            }

            string contactId;

            var existingContact = FindCurrentContact(contactDTO.Login, out contactId);

            if (!existingContact)
            {
                return NotFound();
            }

            dynamic input = new ExpandoObject();
            input.disabled = true;

            string deActivateURL = $"{URL}/{contactId}";

            System.Net.Http.HttpClient client = new System.Net.Http.HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", "Basic " + AuthenticationHeader);

            HttpResponseMessage response = new HttpResponseMessage();
            client.DefaultRequestHeaders.Add("X-HTTP-Method-Override", "PATCH");
            response = await client.PostAsync(deActivateURL, new StringContent(JsonConvert.SerializeObject(input)));

            if (response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode);
            }
            {
                var strResult = response.Content.ReadAsStringAsync().Result;
                _logger.LogError($"Request failed for login {contactDTO.Login}. Reason {strResult}");
                return new BadRequestResult();
            }
        }

        private bool FindCurrentContact(string login, out string contactId)
        {
            contactId = string.Empty;

            System.Net.Http.HttpClient client = new System.Net.Http.HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", "Basic " + AuthenticationHeader);

            var existingContact = client.GetAsync($"{URL}?q=login='{login}'").Result.Content.ReadAsStringAsync().Result;

            var result = JsonConvert.DeserializeObject<dynamic>(existingContact);

            if (result.items.Count == 0)
            {
                return false;
            }

            contactId = result.items[0].id.ToString();

            return true;
        }
    }
}
