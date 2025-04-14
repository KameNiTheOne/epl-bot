from parsers import PageParser
import requests
from bs4 import BeautifulSoup
import os # https://www.consultant.ru/popular/
laws = [["Гражданский кодекс", "https://www.consultant.ru/document/cons_doc_LAW_5142/", 
                               "https://www.consultant.ru/document/cons_doc_LAW_9027/",
                               "https://www.consultant.ru/document/cons_doc_LAW_34154/",
                               "https://www.consultant.ru/document/cons_doc_LAW_64629/"],
        ["Жилищный кодекс", "https://www.consultant.ru/document/cons_doc_LAW_51057/"],
        ["Налоговый кодекс", "https://www.consultant.ru/document/cons_doc_LAW_19671/",
                             "https://www.consultant.ru/document/cons_doc_LAW_28165/"],
        ["Трудовой кодекс", "https://www.consultant.ru/document/cons_doc_LAW_34683/"],
        ["Уголовный кодекс", "https://www.consultant.ru/document/cons_doc_LAW_10699/"],
        ["Админ правонаруш", "https://www.consultant.ru/document/cons_doc_LAW_34661/"],
        ["Бюджетный кодекс", "https://www.consultant.ru/document/cons_doc_LAW_19702/"],
        ["Арбитражный кодекс", "https://www.consultant.ru/document/cons_doc_LAW_37800/"],
        ["Земельный кодекс", "https://www.consultant.ru/document/cons_doc_LAW_33773/"],
        ["О защите потреб", "https://www.consultant.ru/document/cons_doc_LAW_305/"],
        ["Банкрот", "https://www.consultant.ru/document/cons_doc_LAW_39331/"]]
# def extract_blockquote_links(html_content):
#     # Parse the HTML content
#     soup = BeautifulSoup(html_content, 'html.parser')
    
#     # Find the container div
#     container = soup.find('div', class_='container')
#     if not container:
#         return []
    
#     # Find the row div within container
#     row = container.find('div', class_='row')
#     if not row:
#         return []
    
#     # Find the content div within row
#     content = row.find('div', class_="col-pt-9 col-pt-push-3")
#     if not content:
#         return []
    
#     # Find all blockquotes within content
#     blockquotes = content.find_all('blockquote')
    
#     # Extract all href attributes from links in blockquotes
#     links = []
#     for blockquote in blockquotes:
#         # Get all links (a tags) within the blockquote
#         for a_tag in blockquote.find_all('a'):
#             href = a_tag.get('href')
#             if href:  # Only add if href exists
#                 links.append(f"https://www.consultant.ru{href}")
    
#     return links

# def get_final_url(url):
#     try:
#         response = requests.head(url, allow_redirects=True, timeout=5)
#         return response.url
#     except requests.exceptions.RequestException:
#         return url  # Return original if there's an error

# def combatRedirects(links):
#     realLinks = []
#     for link in links:
#         real = get_final_url(link)
#         if link != real:
#             print(f"Original: {link}")
#             print(f"Actual: {real}")
#         html_content = requests.get(real).text
#         soup = BeautifulSoup(html_content, 'html.parser')
#         if not soup.find('div', class_='content error-page'):
#             realLinks.append(real)
#     return realLinks

# # Example usage:
# response = requests.get("https://www.consultant.ru/popular/")

# links = extract_blockquote_links(response.text)
# realLinks = combatRedirects(links)

# # Print the results
# print(f"Total links found: {len(links)}")
# print("\nFirst 10 links:")
# realLinks = list(set(realLinks))
# print(f"Non-repeating links amount: {len(realLinks)}")
# with open("./urls.txt", "+a") as f:
#     for i in realLinks:
#         f.write(f"{i}\n")
parsed = [i.strip() for i in open("./parsed.txt").readlines()]
parsed.append("remove")
PageParser.parsedUrls = set(parsed)
links = [i.strip() for i in open("./urls.txt").readlines()]
numberedLinks = list(zip([i+9 for i in range(len(links))], links))
for link in numberedLinks[:10]:
    print(link)
print(numberedLinks[-1])

savePath = "C:\\codes\\epl_embedder\\docs\\to_load\\" # C:\\codes\\epl_embedder\\docs\\to_load\\
for l in numberedLinks:
    stuff = PageParser.FirstPageSearch(l[1]) # https://www.consultant.ru/document/cons_doc_LAW_5142/aa87cfbfdb5358dce8542e6c9b4b0593639d20e9/
    if not os.path.exists(f"{savePath}{l[0]}"):
        os.makedirs(f"{savePath}{l[0]}")
    PageParser.recursive_save(stuff, f"{savePath}{l[0]}\\")