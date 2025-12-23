using NetCoreServer;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WorldsAdriftServer.Objects.CharacterSelection;

namespace WorldsAdriftServer.Handlers.CharacterScreen
{
    internal static class CharacterSaveHandler
    {
        internal static void HandleCharacterDelete( HttpSession session, HttpRequest request )
        {
            var characterUid = request.Url.Split("/steam/1234/")[1];

            CharacterDatabase.DeleteByCharacterUid(characterUid);
            HttpResponse resp = new HttpResponse();

            resp.SetBegin(200);
            resp.SetBody(JsonConvert.SerializeObject(CharacterListHandler.CreateCharacterListResponse("local_server")));

            session.SendResponseAsync(resp);
        }
        
        internal static void HandleCharacterSave( HttpSession session, HttpRequest request )
        {
            JObject reqO = JObject.Parse(request.Body);
            if (reqO != null)
            {
                CharacterCreationData characterData = reqO.ToObject<CharacterCreationData>();
                if (characterData != null)
                {
                    if (string.IsNullOrEmpty(characterData.characterUid))
                        characterData.characterUid = Guid.NewGuid().ToString();
                    CharacterDatabase.Store(characterData, JsonConvert.SerializeObject(characterData.Cosmetics), JsonConvert.SerializeObject(characterData.UniversalColors));
                    HttpResponse resp = new HttpResponse();

                    resp.SetBegin(200);
                    resp.SetBody(JsonConvert.SerializeObject(CharacterListHandler.CreateCharacterListResponse("local_server")));

                    session.SendResponseAsync(resp);
                }
            }
        }
    }
}
