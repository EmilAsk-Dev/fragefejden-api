using FrageFejden.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace FrageFejden.Controllers.Quiz
{
    [ApiController]
    [Authorize(Roles = "admin,teacher")]
    [Route("api/topics/{topicId:guid}/levels/{levelId:guid}/questions")]
    [Tags("Questions")]
    public class QuestionsController : ControllerBase
    {
        private readonly IQuizService _quizService;

        public QuestionsController(IQuizService quizService)
        {
            _quizService = quizService;
        }

        [HttpPost]
        [SwaggerOperation(summary: "Create a question under a topic/level")]
        [ProducesResponseType(typeof(CreatedQuestionDto), 201)]
        public async Task<ActionResult<CreatedQuestionDto>> Create(
    Guid topicId,
    Guid levelId,
    [FromBody] CreateQuestionDto dto)
        {
            try
            {
                if (!ModelState.IsValid) return BadRequest(ModelState);

                var id = await _quizService.CreateAsync(
                    topicId,
                    levelId,
                    dto.Stem,
                    dto.Explanation,
                    dto.Options.Select(o => (o.Text, o.IsCorrect)).ToList()
                );


                return StatusCode(201, new CreatedQuestionDto { Id = id });
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return BadRequest(ex.Message);
            }
        }

        public class CreateQuestionDto
        {
            [Required] public string Stem { get; set; } = null!;
            public string? Explanation { get; set; }
            [MinLength(2)]
            public List<OptionDto> Options { get; set; } = new();
        }

        public class OptionDto
        {
            [Required] public string Text { get; set; } = null!;
            public bool IsCorrect { get; set; }
        }

        public class CreatedQuestionDto
        {
            public Guid Id { get; set; }
        }
    }
}