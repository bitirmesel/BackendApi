using DktApi.Dtos.Therapist;
using DktApi.Models.Db;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DktApi.Services;

namespace DktApi.Endpoints;

public static class TherapistEndpoints
{
    public static void MapTherapistEndpoints(this WebApplication app)
    {
        // GET: tek terapist
        app.MapGet("/api/therapists/{id:int}", async (int id, AppDbContext db) =>
        {
            var t = await db.Therapists.FindAsync((long)id);
            return t is null ? Results.NotFound() : Results.Ok(t);
        })
        .WithTags("Therapists")
        .WithName("GetTherapist");

        // PUT: profil güncelle (kısmi güncelleme — sadece dolu alanlar yazılır)
        app.MapPut("/api/therapists/{id:int}", async (int id, [FromBody] UpdateTherapistRequest req, AppDbContext db) =>
        {
            var therapist = await db.Therapists.FindAsync((long)id);
            if (therapist is null)
                return Results.NotFound();

            if (!string.IsNullOrWhiteSpace(req.Name))
                therapist.Name = req.Name.Trim();

            if (!string.IsNullOrWhiteSpace(req.Email))
            {
                var newEmail = EmailNormalizer.Normalize(req.Email)!;
                if (newEmail != therapist.Email)
                {
                    var emailTaken = await db.Therapists
                        .AnyAsync(x => x.Id != therapist.Id && x.Email.ToLower() == newEmail);
                    if (emailTaken)
                        return Results.BadRequest(new { message = "Bu e-posta başka bir hesapta kullanılıyor." });
                    therapist.Email = newEmail;
                }
            }

            if (req.PhoneNumber is not null) therapist.PhoneNumber = req.PhoneNumber.Trim();
            if (req.LicenseNumber is not null) therapist.LicenseNumber = req.LicenseNumber.Trim();
            if (req.ProfileImageUrl is not null) therapist.ProfileImageUrl = req.ProfileImageUrl.Trim();
            if (req.ClinicName is not null) therapist.ClinicName = req.ClinicName.Trim();
            if (req.City is not null) therapist.City = req.City.Trim();

            therapist.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();
            return Results.Ok(therapist);
        })
        .WithTags("Therapists")
        .WithName("UpdateTherapist");

        // PUT: şifre değiştir
        app.MapPut("/api/therapists/{id:int}/password", async (int id, [FromBody] ChangePasswordRequest req, AppDbContext db, PasswordService passwords) =>
        {
            if (string.IsNullOrEmpty(req.OldPassword) || string.IsNullOrEmpty(req.NewPassword))
                return Results.BadRequest(new { message = "Eski ve yeni şifre zorunlu." });

            if (req.NewPassword.Length < 6)
                return Results.BadRequest(new { message = "Yeni şifre en az 6 karakter olmalı." });

            var therapist = await db.Therapists.FindAsync((long)id);
            if (therapist is null)
                return Results.NotFound();

            if (!PasswordService.IsSuccess(passwords.Check(therapist.PasswordHash, therapist.Password, req.OldPassword)))
                return Results.BadRequest(new { message = "Eski şifre hatalı." });

            // Yeni şifre yalnızca hash olarak; eski düz metin artık geçersiz (PR-13)
            therapist.PasswordHash = passwords.Hash(req.NewPassword);
            therapist.Password = null;
            therapist.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            return Results.Ok(new { message = "Şifre güncellendi." });
        })
        .WithTags("Therapists")
        .WithName("ChangeTherapistPassword");
    }
}

