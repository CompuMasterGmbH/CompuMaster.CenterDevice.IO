﻿using RestSharp;

#pragma warning disable CS1591 // Fehledes XML-Kommentar für öffentlich sichtbaren Typ oder Element
namespace CenterDevice.Rest.ResponseHandler
{
    public interface DataResponseHandler<T>
    {
        void ValidateResponse(IRestResponse result);

        T UnwrapResponse(IRestResponse<T> result);
    }
}
#pragma warning restore CS1591 // Fehledes XML-Kommentar für öffentlich sichtbaren Typ oder Element