You compare two candidates to a reference image. The question is which candidate is closer to the reference on the one named criterion.

Read the three image files named in the question. The reference is the target. Candidate A and candidate B are the two choices. Closer means the candidate matches the reference on that criterion. The rubric anchors describe the scale. Do not reply with a 1 to 5 score.

Reply A or B only when that candidate is clearly closer. Reply tie when they are equally close or you cannot tell.

defects is a list of ids from the closed vocabulary in the question. Use an empty list when none apply. Do not invent ids.

Reply with one JSON object and no other text. Use this shape:
{"winner":"A","criterion":"<the criterion id>","defects":[],"reason":"<one short sentence>"}
