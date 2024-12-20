using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using backend.DTOs;
using backend.Models.Domain;
using backend.Errors;
using CHESSPROJ.Utilities;
using System.Text.Json;
using Stockfish.NET;
using backend.Data;
using backend.Utilities;
using Microsoft.Extensions.Logging;
using backend.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using backend.Controllers;
using System.Security.Claims;

namespace CHESSPROJ.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChessController : ControllerBase
    {
        private static ErrorMessages gameNotFound = ErrorMessages.Game_not_found;
        private static ErrorMessages badMove = ErrorMessages.Move_notation_cannot_be_empty;

        private static ErrorMessages registeringError = ErrorMessages.Bad_request;
        private readonly IStockfishService _stockfishService;
        private readonly IDatabaseUtilities dbUtilities;
        private readonly IJwtService _jwtService;
        private readonly ILogger<ChessController> logger;

        public int reset;

        // Dependency Injection through constructor
        public ChessController(IStockfishService stockfishService, IDatabaseUtilities dbUtilities, ILogger<ChessController> logger, IJwtService jwtService)
        {
            _stockfishService = stockfishService;
            this.dbUtilities = dbUtilities;
            this.logger = logger;
            _jwtService = jwtService;
        }

        [Authorize]
        [HttpPost("create-game")]
        public async Task<IActionResult> CreateGame([FromBody] CreateGameReqDto req)
        {
            _stockfishService.SetLevel(req.aiDifficulty);
            Game game = Game.CreateGameFactory(Guid.NewGuid(), req.gameDifficulty, req.aiDifficulty, 3);
            game.IsRunning = true;
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            game.UserId = userId;

                       //cia testavimui
            /*
            List<string> initialMoves = new List<string>
            {
                "e2e3", "g7g5", "a2a3", "f7f6"
            };

            // Set the initial moves on Stockfish and the game object
            string initialPosition = string.Join(" ", initialMoves);
            _stockfishService.SetPosition(initialPosition, "");

            game.MovesArraySerialized = JsonSerializer.Serialize(initialMoves);
            */

            try
            {
                if (await dbUtilities.AddGame(game))
                {
                    var PostCreateGameResponseDTO = new PostCreateGameResponseDTO {
                    GameId = game.GameId.ToString()
                };
                    return Ok(PostCreateGameResponseDTO);
                }
                else
                {
                    throw new DatabaseOperationException("Failed to add the game to the database.");
                }
            }
            catch (DatabaseOperationException ex)
            {
                logger.LogError(ex, "error while adding game to database {message}", ex.Message);
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        [Authorize]
        [HttpGet("{gameId}/history")]
        public async Task<IActionResult> GetMovesHistory(string gameId)
        {
            Game game = await dbUtilities.GetGameById(gameId);
            if (game == null)
                return NotFound("Game not found.");

            List<string> moves = new List<string>();
            if (game.MovesArraySerialized != null)
            {
                moves = JsonSerializer.Deserialize<List<string>>(game.MovesArraySerialized);
            }

            var response = new GetMovesHistoryResponseDTO 
            {
                MovesArray = moves
            };

            return Ok(response);
}


        // POST: api/chessgame/{gameId}/move
        [Authorize]
        [HttpPost("{gameId}/move")]
        public async Task<IActionResult> MakeMove(string gameId, [FromBody] MoveDto moveNotation)
        {
            Game game = await dbUtilities.GetGameById(gameId);
            if (game == null)
            {
                return NotFound($"{gameNotFound.ToString()}");
            }

            GameState gameState = await dbUtilities.GetStateById(gameId);
            reset = gameState.CurrentBlackout;
            System.Console.WriteLine("THIS IS RESET" + reset);
            game.Duration = moveNotation.gameTime;
            string move = moveNotation.move;
            // Validate move input
            if (string.IsNullOrEmpty(move))
            {
                return BadRequest($"{badMove.ToString()}");
            }

            List<string> MovesArray = new List<string>();
            if (game.MovesArraySerialized != null)
            {
                MovesArray = JsonSerializer.Deserialize<List<string>>(game.MovesArraySerialized);
            }
            string currentPosition = string.Join(" ", MovesArray);

            if (_stockfishService.IsMoveCorrect(currentPosition, move) && game.IsRunning) //kad nebtuu kokiu shenaningans
            {
                _stockfishService.SetPosition(currentPosition, move);
                MovesArray.Add(move);
                string botMove = _stockfishService.GetBestMove();
                _stockfishService.SetPosition(string.Join(" ", MovesArray), botMove);
                MovesArray.Add(botMove);
                string fenPosition = _stockfishService.GetFen();
                currentPosition = string.Join(" ", MovesArray);

                gameState.HandleBlackout();

                game.MovesArraySerialized = JsonSerializer.Serialize(MovesArray);

                if(_stockfishService.GetEvalType() == "mate"){
                    System.Console.WriteLine(_stockfishService.GetEvalVal());
                    System.Console.WriteLine(_stockfishService.GetEvalType());
                    if(_stockfishService.GetEvalVal() >= 0){
                        //reiskia baltas padare mate
                        gameState.WLD = 1;
                    }else{
                        //reiskia juodas padare mate
                        gameState.WLD = 0;
                    }
                    //nu jei mate tai game tikrai over
                    game.IsRunning = false; 
                    await dbUtilities.UpdateGame(game, gameState);
                        var postMoveResponseDTO = new PostMoveResponseDTO {
                            WrongMove = false,
                            BotMove = botMove,
                            Lives = gameState.CurrentLives,
                            IsRunning = game.IsRunning,
                            TurnBlack = false, //kad matytum final board jei buvo mate
                            FenPosition = fenPosition,
                            GameWLD = (int)gameState.WLD,
                            CurrentPosition = currentPosition,
                            Duration = game.Duration
                        };
                        return Ok(postMoveResponseDTO);
                }else{
                    await dbUtilities.UpdateGame(game, gameState);  
                    var postMoveResponseDTO = new PostMoveResponseDTO {
                        WrongMove = false,
                        BotMove = botMove,
                        IsRunning = true,
                        CurrentPosition = currentPosition,
                        FenPosition = fenPosition,
                        TurnBlack = gameState.TurnBlack
                    };
                    return Ok(postMoveResponseDTO);
                }
            }
            else
            {
                gameState.CurrentLives--; //minus life
                if (gameState.CurrentLives <= 0)
                {
                    game.IsRunning = false;
                    gameState.CurrentLives = 0; //kad nebutu negative in db
                    gameState.WLD = 0;

                }
                gameState.HandleBlackout();

                var postMoveResponseDTO = new PostMoveResponseDTO {
                        WrongMove = true,
                        Lives = gameState.CurrentLives,
                        IsRunning = game.IsRunning,
                        TurnBlack = gameState.TurnBlack,
                        GameWLD = (int)gameState.WLD,
                        Duration = game.Duration

                };

                await dbUtilities.UpdateGame(game, gameState);
                return Ok(postMoveResponseDTO); // we box here :) (fight club reference)
            }
        }
         
        [Authorize]
        [HttpGet("games")]
        public async Task<IActionResult> GetUserGames()
        {
            GamesList gamesList = new GamesList(await dbUtilities.GetGamesList());
            List<Game> userGames = new List<Game>();
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            foreach (var game in gamesList)
            {
                if (game.UserId.ToString() == userId)
                {
                    userGames.Add(game);
                }
            }

            return Ok(userGames);
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (await dbUtilities.AddUser(model))
            {
                return Ok(new { message = "Registration successful" });
            }

            if (model.Password != model.ConfirmPassword)
            {
                return BadRequest(new { message = "Passwords don't match" });
            }
            else if (model.Password.ToString().Length <= 8)
            {
                return BadRequest(new { message = "Password is too short needs to be at least 4 characters" });
            }
            else if (!model.Password.ToString().Any(char.IsDigit) || !model.Password.ToString().Any(char.IsUpper) || !model.Password.ToString().Any(char.IsLower))
            {
                return BadRequest(new { message = "Password needs to have a number, an uppercase and a lowercase" });
            }
            else if (await dbUtilities.FindIfUsernameExists(model))
            {
                return BadRequest(new { message = "Username already taken" });
            }
            else if (await dbUtilities.FindIfEmailExists(model))
            {
                return BadRequest(new { message = "Email already taken" });
            }
            return BadRequest($"{registeringError.ToString()}");
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (await dbUtilities.LogInUser(model))
            {
                var user = await dbUtilities.GetUserByEmail(model);
                var token = _jwtService.GenerateToken(user);
                return Ok(new { token, user.UserName, user.Email });
            }
            else
            {
                return BadRequest("Invalid credentials");
            }
        }
    }
}