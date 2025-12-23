using NetCoreServer;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WorldsAdriftServer.Helper.CharacterSelection;
using WorldsAdriftServer.Objects.CharacterSelection;

namespace WorldsAdriftServer.Handlers.CharacterScreen
{
    internal static class CharacterListHandler
    {
        internal static void HandleReserveName( HttpSession session, HttpRequest request )
        {
            var failReasons = new List<string>();
            JObject requestObject;
            try
            {
                requestObject = JObject.Parse(request.Body);
                if (requestObject == null)
                {
                    failReasons.Add("Invalid reservation body");
                    goto Fail;
                }
            }
            catch (Exception ex)
            {
                failReasons.Add($"Parsing error: {ex.Message}");
                goto Fail;
            }

            var name = requestObject.Value<string?>("screenName");
            if (string.IsNullOrEmpty(name))
            {
                failReasons.Add("Cannot submit empty screen name!");
                goto Fail;
            }

            if (CharacterDatabase.IsCharacterNameTaken(name))
            {
                failReasons.Add("Character with this name already exists!");
                goto Fail;
            }

            HttpResponse resp = new HttpResponse();
            resp.SetBegin(200);
            resp.SetBody("{}");

            session.SendResponseAsync(resp);

            return;
            Fail:
            HttpResponse fail = new HttpResponse();
            fail.SetBegin(400);
            ;
            fail.SetBody("{\"desc\": \"" + JsonConvert.SerializeObject(failReasons) + "\"}");

            session.SendResponseAsync(fail);
            return;
        }

        internal static void HandleReserveSlot( HttpSession session, HttpRequest request )
        {
            HttpResponse resp = new HttpResponse();
            resp.SetBegin(200);
            resp.SetBody(JsonConvert.SerializeObject(CreateCharacterListResponse("local_server")));

            session.SendResponseAsync(resp);
        }

        public static CharacterListResponse CreateCharacterListResponse( string serverIdentifier )
        {
            const int MaxSlots = 6;

            var list = CharacterDatabase.GetAllCharacters();

            var usedIndices = list.Select(c => c.Id).ToHashSet();

            for (int slot = 0; slot < MaxSlots; slot++)
            {
                if (usedIndices.Contains(slot))
                {
                    continue;
                }

                list.Add(Character.GenerateNewCharacter(slot, serverIdentifier, ""));
            }

            list = list.OrderBy(c => c.Id).ToList();

            var characterList = new CharacterListResponse(list)
            {
                unlockedSlots = MaxSlots, 
                hasMainCharacter = usedIndices.Count > 0,
                havenFinished = true
            };

            return characterList;
        }

        /*
         * URL: /characterList/{buildNumber}/steam/1234
         *
         * once the user clicks on the play button the game requests a list of characters.
         * the response also decides whether there is an option to create a new character using the unlockedSlots field
         */
        internal static void HandleCharacterListRequest( HttpSession session, HttpRequest request,
            string serverIdentifier )
        {
            // var list = CharacterDatabase.GetAllCharacters();
            //
            // list.Add(Character.GenerateNewCharacter(serverIdentifier, ""));
            //
            // CharacterListResponse characterList = new CharacterListResponse(list);
            // characterList.unlockedSlots = list.Count; // let the player create a new character below the list of existing characters (last provided character above must be a GenerateNewCharacter())
            // characterList.hasMainCharacter = true;
            // characterList.havenFinished = true;

            var characterList = CreateCharacterListResponse(serverIdentifier);

            JObject respO = (JObject)JToken.FromObject(characterList);
            if (respO != null)
            {
                HttpResponse resp = new HttpResponse();
                resp.SetBegin(200);
                resp.SetBody(respO.ToString());

                session.SendResponseAsync(resp);
            }
        }
    }
}
