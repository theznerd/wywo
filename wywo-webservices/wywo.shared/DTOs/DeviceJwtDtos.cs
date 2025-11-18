using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace wywo.shared.DTOs
{
    public sealed record DeviceEnrollmentDto(
        int V,
        string DeviceId,
        string Algo,
        string Jwk,
        string? Kid,
        string? DeviceName,
        string Nonce // generated at device enrollment, echoed back in ack to validate completion
    );

    public sealed record DeviceEnrollmentResultDto(
        string DeviceId,
        string AckJwt, // JWT to be sent back to device to acknowledge enrollment, includes nonce value
        DateTimeOffset AckExpiresAt
    );
}
