import os
from typing import List
from dotenv import load_dotenv
import requests
import json


# Step 1 - Define the Json Models
class Message:
    def __init__(self, Role: str, Content: str):
        self.role = Role
        self.content = Content

    def __dict__(self):
        return {"role": self.role, "content": self.content}


class Prompt:
    def __init__(self, Messages: List[Message], Max_tokens: int, Temperature: float):
        self.messages = Messages
        self.max_tokens = Max_tokens
        self.temperature = Temperature

    def __dict__(self):
        return {
            "messages": [message.__dict__() for message in self.messages],
            "max_tokens": self.max_tokens,
            "temperature": self.temperature,
        }


# Step 2 - Initial Configuration
# TODO: Add a .env file and add the API KEY and URI
START_SYSTEM_MESSAGE = "You are a general assistant"
history = []

# Step 3 - Load the Environment Variables
load_dotenv()
uri = os.getenv("OPENAI_URI")
api_key = os.getenv("OPENAI_KEY")

# Step 4 - Configure the Http Client headers
headers = {"Content-Type": "application/json", "api-key": f"{api_key}"}


# Step 5 - Define the Post Method
def post(history: List[Message]):
    prompt = json.dumps(Prompt(history, 100, 0.3).__dict__())
    response = requests.post(uri, data=prompt, headers=headers)
    # Handle the response
    if response.status_code == 200:
        response_data = response.json()
        return (
            response_data.get("choices")[0].get("message").get("content"),
            response_data.get("usage").get("prompt_tokens"),
            response_data.get("usage").get("completion_tokens"),
        )
    else:
        print("Error:", response.status_code, response.text)
        return (None, 0, 0)


def run():
    history.append(Message("system", START_SYSTEM_MESSAGE))
    completion, tin, tout = post(history)
    while True:
        prompt = input(f"\nTokens In: {tin} Out: {tout} --> What is your question?\n\n")
        if prompt == "quit":
            print("Goodbye!")
            break
        elif prompt == "history":
            continue
        else:
            history.append(Message("user", prompt))
            completion, ptin, ptout = post(history)
            tin += ptin
            tout += ptout
            print("\n" + completion)
            history.append(Message("system", completion))


if __name__ == "__main__":
    # Step 6 - Run the application loop
    run()
