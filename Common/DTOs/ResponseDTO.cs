using System.Text.Json;

namespace Common.DTOs;

public class ResponseDTO
{
    public int StatusCode { get; set; }
    public string? Message { get; set; } = string.Empty;
    public bool IsSuccess { get; set; }
    public object? Result { get; set; }



    public override string ToString() => JsonSerializer.Serialize(this);

    public ResponseDTO(string message, int statusCode, bool success = false, object? result = null)
    {
        Message = message;
        StatusCode = statusCode;
        IsSuccess = success;
        Result = result;
    }
}