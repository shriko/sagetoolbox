using System.Security.Cryptography;
using System.Text;

namespace SageToolbox.Core;

/// <summary>
/// Mot de passe d'accès applicatif (gate avant la fenêtre principale). Portage fidèle de
/// verify_pwd (AIG_Toolbox.py V1) : MD5(mot de passe) comparé au hash hexadécimal
/// hardcodé HASHED_PASSWORD. Conservé tel quel (même algorithme, même hash) pour que le
/// mot de passe déjà en usage dans l'équipe continue de fonctionner sans redistribution —
/// SYNTHESE_V2.md décrivait un mécanisme PBKDF2-HMAC-SHA256 qui ne correspond pas au
/// source V1 réel (probablement une confusion, ou un renforcement V2 dont je n'ai pas le
/// source). MD5 est cryptographiquement faible ; si l'équipe souhaite migrer vers un
/// hash plus robuste, il faudra alors choisir et redistribuer un nouveau mot de passe.
/// </summary>
public static class PasswordHasher
{
    private const string HashedPassword = "86ec849171b8ffcf7e2505c84fa63b0e";

    public static bool Verify(string password)
    {
        var hashBytes = MD5.HashData(Encoding.UTF8.GetBytes(password));
        var hex = Convert.ToHexString(hashBytes).ToLowerInvariant();
        return hex == HashedPassword;
    }
}
