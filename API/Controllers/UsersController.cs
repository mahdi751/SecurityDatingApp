using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.DTOs;
using API.Entities;
using API.Extensions;
using API.Helpers;
using API.Interfaces;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;


using nClam;

namespace API.Controllers
{
    [Authorize]
    public class UsersController : BaseApiController
    {
        private readonly IUnitOfWork _uow;
        private readonly IMapper _mapper;
        private readonly IPhotoService _photoService;
        private readonly ILogger<UsersController> _logger; 

        public UsersController(IUnitOfWork uow, IMapper mapper, IPhotoService photoService,ILogger<UsersController> logger)
        {
            _photoService = photoService;
            _uow = uow;
            _mapper = mapper;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<MemberDto>>> GetUsers([FromQuery] UserParams userParams)
        {
            var currentUser = await _uow.UserRepository.GetUserByUsernameAsync(User.GetUsername());
            userParams.CurrentUsername = currentUser.UserName;

            if (string.IsNullOrEmpty(userParams.Gender))
                userParams.Gender = currentUser.Gender == "male" ? "female" : "male";

            var users = await _uow.UserRepository.GetMembersAsync(userParams);

            Response.AddPaginationHeader(new PaginationHeader(users.CurrentPage, users.PageSize,
                users.TotalCount, users.TotalPages));

            return Ok(users);
        }

        [HttpGet("{username}")]
        public async Task<ActionResult<MemberDto>> GetUser(string username)
        {
            return await _uow.UserRepository.GetMemberAsync(username);
        }

        [HttpPut]
        public async Task<ActionResult> UpdateUser(MemberUpdateDto memberUpdateDto)
        {
            var user = await _uow.UserRepository.GetUserByUsernameAsync(User.GetUsername());

            _mapper.Map(memberUpdateDto, user);

            _uow.UserRepository.Update(user);

            if (await _uow.Complete()) return NoContent();

            return BadRequest("Failed to update user");
        }

        // private async Task<bool> IsFileCleanAsync(byte[] fileBytes)
        // {
        //     ClamClient clam = null;
        //     try
        //     {
        //         clam = new ClamClient("127.0.0.1", 3310);
        //         var scanResult = await clam.SendAndScanFileAsync(fileBytes);
        //         return scanResult.Result == ClamScanResults.Clean;
        //     }
        //     finally
        //     {
        //         // Make sure to dispose of the ClamClient instance
        //         clam?.Dispose();
        //     }
        // }


        [HttpPost("add-photo")]
        public async Task<ActionResult<PhotoDto>> AddPhoto(IFormFile file)
        {
            var user = await _uow.UserRepository.GetUserByUsernameAsync(User.GetUsername());
            ClamScanResult scanResult = null;

            if (file == null || file.Length == 0)  
                return Content("file not selected");  

            var ms = new MemoryStream();  
            file.OpenReadStream().CopyTo(ms);  
            byte[] fileBytes = ms.ToArray();  

            try  
            {  
                this._logger.LogInformation("ClamAV scan begin for file {0}", file.FileName);  
                var clam = new ClamClient("localhost", 3310);

                scanResult = await clam.SendAndScanFileAsync(fileBytes);    
                if (scanResult != null)
                {
                    switch (scanResult.Result)  
                    {  
                        case ClamScanResults.Clean:  
                            this._logger.LogInformation("The file is clean! ScanResult:{1}", scanResult.RawResult);  
                            break;  
                        case ClamScanResults.VirusDetected:  
                            this._logger.LogError("Virus Found! Virus name: {1}", scanResult.InfectedFiles.FirstOrDefault()?.VirusName);  
                            break;  
                        case ClamScanResults.Error:  
                            this._logger.LogError("An error occurred while scanning the file! ScanResult: {1}", scanResult.RawResult);  
                            break;  
                        case ClamScanResults.Unknown:  
                            this._logger.LogError("Unknown scan result while scanning the file! ScanResult: {0}", scanResult.RawResult);  
                            break;  
                    }
                }
                else
                {
                    this._logger.LogError("ClamAV scan result is null.");
                }

            } 
            catch (Exception ex)  
            {  
                this._logger.LogError("ClamAV Scan Exception: {0}", ex.ToString());  
            }  
            this._logger.LogInformation("ClamAV scan completed for file {0}", file.FileName);

            if (scanResult != null && scanResult.Result == ClamScanResults.Clean)
            {
                var result = await _photoService.AddPhotoAsync(file);

                if (result.Error != null) return BadRequest(result.Error.Message);

                var photo = new Photo
                {
                    Url = result.SecureUrl.AbsoluteUri,
                    PublicId = result.PublicId
                };

                if (user.Photos.Any(existingPhoto => existingPhoto.Url == photo.Url))
                {
                    return BadRequest("This photo is already in the database.");
                }

                using (var stream = file.OpenReadStream())
                {
                    foreach (var dbPhoto in user.Photos)
                    {
                        using (var webClient = new WebClient())
                        {
                            using (var dbPhotoStream = new MemoryStream(webClient.DownloadData(dbPhoto.Url)))
                            {
                                var similarity = _photoService.CalculateImageSimilarity(stream, dbPhotoStream);

                                if (similarity > 0.65)
                                {
                                    _logger.LogInformation("Similarity is :"+similarity);
                                    _logger.LogError("Similar photo already exists in the database.");
                                    return BadRequest();
                                }
                                else{
                                    _logger.LogInformation("Similarity is :"+similarity);
                                }
                            }
                        }
                    }
                }





                if (user.Photos.Count == 0) photo.IsMain = true;

                user.Photos.Add(photo);

                if (await _uow.Complete())
                    return CreatedAtAction(nameof(GetUser), new { username = user.UserName },
                        _mapper.Map<PhotoDto>(photo));

                return BadRequest("Problem adding photo");
            }
            else
            {
                return BadRequest("Problem adding photo");
            }
        }

        [HttpPut("set-main-photo/{photoId}")]
        public async Task<ActionResult> SetMainPhoto(int photoId)
        {
            var user = await _uow.UserRepository.GetUserByUsernameAsync(User.GetUsername());

            var photo = user.Photos.FirstOrDefault(x => x.Id == photoId);

            if (photo == null) return NotFound();

            if (photo.IsMain) return BadRequest("This is already your main photo");

            var currentMain = user.Photos.FirstOrDefault(x => x.IsMain);
            if (currentMain != null) currentMain.IsMain = false;
            photo.IsMain = true;

            if (await _uow.Complete()) return NoContent();

            return BadRequest("Problem setting the main photo");
        }

        [HttpDelete("delete-photo/{photoId}")]
        public async Task<ActionResult> DeletePhoto(int photoId)
        {
            var user = await _uow.UserRepository.GetUserByUsernameAsync(User.GetUsername());

            var photo = user.Photos.FirstOrDefault(x => x.Id == photoId);

            if (photo == null) return NotFound();

            if (photo.IsMain) return BadRequest("You cannot delete your main photo");

            if (photo.PublicId != null)
            {
                var result = await _photoService.DeletePhotoAsync(photo.PublicId);
                if (result.Error != null) return BadRequest(result.Error.Message);
            }

            user.Photos.Remove(photo);

            if (await _uow.Complete()) return Ok();

            return BadRequest("Problem deleting the photo");
        }
    }
}
